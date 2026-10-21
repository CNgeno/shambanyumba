using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Depends on: DbContext, INumberSequenceService, TimeProvider.</summary>
public sealed class InvoiceService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    TimeProvider clock) : IInvoiceService
{
    private DateOnly Today => clock.TodayEat();

    // ------------------------------------------------------------------ queries

    public async Task<IReadOnlyList<InvoiceSummary>> ListAsync(InvoiceListFilter filter, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.Invoices.AsNoTracking();
        query = filter.View switch
        {
            InvoiceListView.Draft => query.Where(i => i.Status == InvoiceStatus.Draft),
            InvoiceListView.Unpaid => query.Where(i => i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total),
            InvoiceListView.Overdue => query.Where(i => i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total && i.DueDate < today),
            InvoiceListView.Cancelled => query.Where(i => i.Status == InvoiceStatus.Cancelled || i.Status == InvoiceStatus.WrittenOff),
            _ => query
        };
        if (filter.Month is DateOnly month)
        {
            var (start, end) = BillingRules.MonthBounds(month.Year, month.Month);
            query = query.Where(i => i.InvoiceDate >= start && i.InvoiceDate <= end);
        }
        if (filter.AssetId is int assetId) query = query.Where(i => i.Lease != null && i.Lease.Unit.AssetId == assetId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term) || i.Tenant.FullName.Contains(term)
                                     || (i.Lease != null && i.Lease.Unit.UnitNumber.Contains(term)));
        }

        var rows = await Project(query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).Take(1000)).ToListAsync(ct);
        return rows.Select(r => r with { Status = BillingRules.EffectiveStatus(r.Status, r.Total, r.AmountPaid, r.DueDate, today) }).ToList();
    }

    public async Task<IReadOnlyList<InvoiceSummary>> ListForLeaseAsync(int leaseId, CancellationToken ct = default)
    {
        var today = Today;
        var rows = await Project(db.Invoices.AsNoTracking().Where(i => i.LeaseId == leaseId).OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id))
            .ToListAsync(ct);
        return rows.Select(r => r with { Status = BillingRules.EffectiveStatus(r.Status, r.Total, r.AmountPaid, r.DueDate, today) }).ToList();
    }

    public async Task<InvoiceDetails?> GetDetailsAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Tenant)
            .Include(i => i.Lease).ThenInclude(l => l!.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return null;

        var lines = await db.InvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.SortOrder).ToListAsync(ct);
        return new InvoiceDetails(invoice,
            BillingRules.EffectiveStatus(invoice.Status, invoice.Total, invoice.AmountPaid, invoice.DueDate, Today), lines);
    }

    public async Task<BillingSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var today = Today;
        var (monthStart, monthEnd) = BillingRules.MonthBounds(today.Year, today.Month);
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Draft)
            .Select(i => new { i.Status, i.Total, i.AmountPaid, i.DueDate, i.InvoiceDate })
            .ToListAsync(ct);
        var issued = rows.Where(r => r.Status == InvoiceStatus.Pending).ToList();
        var overdue = issued.Where(r => r.AmountPaid < r.Total && r.DueDate < today).ToList();

        return new BillingSummary(
            InvoicedThisMonth: issued.Where(r => r.InvoiceDate >= monthStart && r.InvoiceDate <= monthEnd).Sum(r => r.Total),
            Outstanding: issued.Sum(r => Math.Max(0, r.Total - r.AmountPaid)),
            OverdueCount: overdue.Count,
            OverdueAmount: overdue.Sum(r => r.Total - r.AmountPaid),
            DraftCount: rows.Count(r => r.Status == InvoiceStatus.Draft),
            IssuedCount: issued.Count);
    }

    // ------------------------------------------------------------------ manual invoices

    public async Task<Result<int>> CreateManualAsync(ManualInvoiceInput input, bool issue, CancellationToken ct = default)
    {
        var error = await ValidateAsync(input, ct);
        if (error is not null) return Result<int>.Fail(error);

        var number = await numbers.NextAsync(SequenceKeys.Invoice, ct);
        var invoice = new Invoice
        {
            InvoiceNumber = Format(number), Kind = InvoiceKind.Manual, TenantId = input.TenantId, LeaseId = input.LeaseId,
            InvoiceDate = input.InvoiceDate, DueDate = input.DueDate, Notes = input.Notes?.Trim()
        };
        foreach (var l in input.Lines)
            invoice.Lines.Add(InvoiceComposer.Line(l.LineType, l.Description.Trim(), l.Quantity, l.UnitPrice, l.TaxPercent));
        InvoiceComposer.Recalculate(invoice);
        if (issue) MarkIssued(invoice);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(invoice.Id);
    }

    public async Task<Result> UpdateDraftAsync(int invoiceId, ManualInvoiceInput input, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft) return Result.Fail("Only draft invoices can be edited. Cancel and re-issue instead.");
        if (invoice.Kind != InvoiceKind.Manual) return Result.Fail("Generated invoices can't be edited line by line. Delete the draft and generate again.");

        var error = await ValidateAsync(input, ct);
        if (error is not null) return Result.Fail(error);

        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.Lines.Clear();
        foreach (var l in input.Lines)
            invoice.Lines.Add(InvoiceComposer.Line(l.LineType, l.Description.Trim(), l.Quantity, l.UnitPrice, l.TaxPercent));

        invoice.TenantId = input.TenantId;
        invoice.LeaseId = input.LeaseId;
        invoice.InvoiceDate = input.InvoiceDate;
        invoice.DueDate = input.DueDate;
        invoice.Notes = input.Notes?.Trim();
        InvoiceComposer.Recalculate(invoice);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>The early-termination penalty from an ended lease, as a final invoice (once).</summary>
    public async Task<Result<int>> CreateFinalInvoiceAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result<int>.Fail("Lease not found.");
        if (lease.Status != LeaseStatus.Terminated || lease.TerminationPenalty is not > 0)
            return Result<int>.Fail("Only terminated leases with a penalty need a final invoice.");
        if (await db.Invoices.AnyAsync(i => i.LeaseId == leaseId && i.Kind == InvoiceKind.Final && i.Status != InvoiceStatus.Cancelled, ct))
            return Result<int>.Fail("A final invoice already exists for this lease.");

        var number = await numbers.NextAsync(SequenceKeys.Invoice, ct);
        var invoice = new Invoice
        {
            InvoiceNumber = Format(number), Kind = InvoiceKind.Final, TenantId = lease.TenantId, LeaseId = lease.Id,
            InvoiceDate = Today, DueDate = Today.AddDays(7),
            Notes = $"Final invoice for lease {lease.LeaseNumber}, ended {lease.EndedOn:d MMM yyyy}."
        };
        invoice.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Penalty,
            $"Early termination penalty ({lease.TerminationReason})", 1, lease.TerminationPenalty.Value, 0));
        InvoiceComposer.Recalculate(invoice);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(invoice.Id);
    }

    // ------------------------------------------------------------------ lifecycle

    public async Task<Result> IssueAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft) return Result.Fail("Only drafts can be issued.");
        if (invoice.Total <= 0) return Result.Fail("An invoice must total more than zero to be issued.");

        MarkIssued(invoice);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> IssueAllDraftsAsync(CancellationToken ct = default)
    {
        var drafts = await db.Invoices.Where(i => i.Status == InvoiceStatus.Draft && i.Total > 0).ToListAsync(ct);
        foreach (var d in drafts) MarkIssued(d);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(drafts.Count);
    }

    public async Task<Result> DeleteDraftAsync(int invoiceId, CancellationToken ct = default)
    {
        var invoice = await db.Invoices.Include(i => i.Lines).FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft) return Result.Fail("Only drafts can be deleted. Cancel an issued invoice instead.");

        await ReleaseReadingsAsync(invoiceId, ct);
        db.InvoiceLines.RemoveRange(invoice.Lines);
        db.Invoices.Remove(invoice);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int invoiceId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for cancelling.");
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending) return Result.Fail("Only issued invoices can be cancelled.");
        if (invoice.AmountPaid > 0) return Result.Fail("This invoice has payments against it. Reverse those payments first.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.StatusReason = reason.Trim();
        await ReleaseReadingsAsync(invoiceId, ct);   // readings can be billed again on a corrected invoice
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> WriteOffAsync(int invoiceId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for writing this off.");
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice is null) return Result.Fail("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending || invoice.AmountPaid >= invoice.Total)
            return Result.Fail("Only issued invoices with a balance can be written off.");

        invoice.Status = InvoiceStatus.WrittenOff;
        invoice.StatusReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ helpers

    public static string Format(long number) => $"INV-{number:00000}";

    private void MarkIssued(Invoice invoice)
    {
        invoice.Status = InvoiceStatus.Pending;
        invoice.IssuedAtUtc = clock.GetUtcNow().UtcDateTime;
    }

    private async Task ReleaseReadingsAsync(int invoiceId, CancellationToken ct)
    {
        var readings = await db.UtilityMeterReadings.Where(r => r.BilledInvoiceId == invoiceId).ToListAsync(ct);
        foreach (var r in readings) r.BilledInvoiceId = null;
    }

    private async Task<string?> ValidateAsync(ManualInvoiceInput input, CancellationToken ct)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == input.TenantId, ct)) return "Choose a tenant.";
        if (input.LeaseId is int leaseId && !await db.Leases.AnyAsync(l => l.Id == leaseId && l.TenantId == input.TenantId, ct))
            return "That lease doesn't belong to the chosen tenant.";
        if (input.DueDate < input.InvoiceDate) return "The due date can't be before the invoice date.";
        return InvoiceComposer.ValidateLines(input.Lines);
    }

    private static IQueryable<InvoiceSummary> Project(IQueryable<Invoice> query)
        => query.Select(i => new InvoiceSummary(i.Id, i.InvoiceNumber, i.Kind, i.TenantId, i.Tenant.FullName, i.LeaseId,
            i.Lease != null ? i.Lease.Unit.UnitNumber : null, i.Lease != null ? i.Lease.Unit.Asset.Name : null,
            i.InvoiceDate, i.DueDate, i.PeriodStart, i.Total, i.AmountPaid, i.Status));
}
