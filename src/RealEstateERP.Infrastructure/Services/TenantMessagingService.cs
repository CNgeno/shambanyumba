using System.Globalization;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Invoice and receipt emails to tenants. Depends on: DbContext, IEmailService, ICurrentUserService, TimeProvider.
/// Sent from the platform address with Reply-To set to the business, so tenants' replies reach the landlord.
/// </summary>
public sealed class TenantMessagingService(
    ApplicationDbContext db,
    IEmailService email,
    ICurrentUserService currentUser,
    TimeProvider clock) : ITenantMessagingService
{
    private static string Kes(decimal v) => "KES " + v.ToString("N2", CultureInfo.InvariantCulture);

    public async Task<Result> SendInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.Include(i => i.Tenant).Include(i => i.Lease!).ThenInclude(l => l.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending) return Result.Fail("Only issued invoices can be emailed.");
        if (string.IsNullOrWhiteSpace(invoice.Tenant.Email)) return Result.Fail($"{invoice.Tenant.FullName} has no email address. Add one on the tenant's page.");

        var lines = await db.InvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.Id).ToListAsync(ct);
        var biz = await BusinessAsync(ct);
        var balance = invoice.Total - invoice.AmountPaid;

        var rows = lines.Select(l => (l.Description, Kes(l.LineTotal), false)).ToList();
        rows.Add(("Total", Kes(invoice.Total), true));
        if (invoice.AmountPaid > 0)
        {
            rows.Add(("Paid", Kes(invoice.AmountPaid), false));
            rows.Add(("Balance due", Kes(balance), true));
        }

        var where = invoice.Lease is null ? "" : $" for {invoice.Lease.Unit.UnitNumber}, {invoice.Lease.Unit.Asset.Name}";
        var html = EmailTemplates.RenderWithRows(biz.Name, $"Invoice {invoice.InvoiceNumber}",
            [$"Hi {FirstName(invoice.Tenant.FullName)}, here is your invoice{where}.",
             $"Please pay {Kes(balance)} by {invoice.DueDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}."],
            rows,
            biz.PaymentInstructions is null ? null : "How to pay:\n" + biz.PaymentInstructions,
            $"Questions? Reply to this email to reach {biz.Name}.{(biz.Phone is null ? "" : $" Phone: {KenyanPhone.Display(biz.Phone)}.")}");

        var sent = await SendAsync(new EmailMessage(invoice.Tenant.Email!, $"Invoice {invoice.InvoiceNumber} from {biz.Name}: {Kes(balance)} due {invoice.DueDate:d MMM}", html, biz.Email), ct);
        if (!sent.Succeeded) return sent;
        invoice.EmailedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SendReceiptAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await db.Payments.Include(p => p.Tenant).FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status != PaymentStatus.Active) return Result.Fail("A reversed payment's receipt can't be emailed.");
        if (string.IsNullOrWhiteSpace(payment.Tenant.Email)) return Result.Fail($"{payment.Tenant.FullName} has no email address. Add one on the tenant's page.");

        var allocations = await db.PaymentAllocations.AsNoTracking().Where(a => a.PaymentId == paymentId && !a.IsReversed)
            .Select(a => new { a.Invoice.InvoiceNumber, a.Amount }).ToListAsync(ct);
        var owing = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == payment.TenantId && i.Status == InvoiceStatus.Pending)
            .SumAsync(i => (decimal?)(i.Total - i.AmountPaid), ct) ?? 0m;
        var biz = await BusinessAsync(ct);

        var rows = new List<(string, string, bool)> { ("Amount received", Kes(payment.Amount), true) };
        rows.Add(("Paid by", payment.Method switch { PaymentMethod.Mpesa => "M-PESA", PaymentMethod.BankTransfer => "Bank transfer", var m => m.ToString() }
            + (payment.Reference is null ? "" : $" {payment.Reference}"), false));
        rows.Add(("Date", payment.PaymentDate.ToString("d MMMM yyyy", CultureInfo.InvariantCulture), false));
        rows.AddRange(allocations.Select(a => ($"Applied to {a.InvoiceNumber}", Kes(a.Amount), false)));
        rows.Add((owing > 0 ? "Still owing" : "Your account", owing > 0 ? Kes(owing) : "Fully paid", true));

        var html = EmailTemplates.RenderWithRows(biz.Name, $"Receipt {payment.ReceiptNumber}",
            [$"Hi {FirstName(payment.Tenant.FullName)}, thank you. We've received your payment."], rows, null,
            $"Keep this email as your receipt. Questions? Reply to reach {biz.Name}.");

        var sent = await SendAsync(new EmailMessage(payment.Tenant.Email!, $"Receipt {payment.ReceiptNumber} from {biz.Name}: {Kes(payment.Amount)} received", html, biz.Email), ct);
        if (!sent.Succeeded) return sent;
        payment.ReceiptEmailedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<string?> AutoSendReceiptAsync(int paymentId, CancellationToken ct = default)
    {
        var id = currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");
        if (!await db.Businesses.AsNoTracking().AnyAsync(b => b.Id == id && b.EmailReceiptsToTenants, ct)) return null;
        var email = await db.Payments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.Tenant.Email).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email)) return "The tenant has no email address, so no receipt was emailed.";
        var sent = await SendReceiptAsync(paymentId, ct);
        return sent.Succeeded ? $"Receipt emailed to {email}." : sent.Error;
    }

    // ---------------------------------------------------------------------

    private sealed record BusinessInfo(string Name, string? Email, string? Phone, string? PaymentInstructions);

    private async Task<BusinessInfo> BusinessAsync(CancellationToken ct)
    {
        var id = currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");
        return await db.Businesses.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new BusinessInfo(b.Name, b.Email, b.Phone, b.PaymentInstructions)).FirstAsync(ct);
    }

    private async Task<Result> SendAsync(EmailMessage message, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(message, ct);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            return Result.Fail($"The email couldn't be sent: {ex.Message}");
        }
    }

    private static string FirstName(string name) => name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
}
