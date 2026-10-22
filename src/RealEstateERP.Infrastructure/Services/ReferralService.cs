using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Rewards at a referred business's first approved subscription payment, added to the approval's own save:
/// the partner's commission (once per business), and for customer referrers, 1 month free on their subscription.
/// </summary>
public static class ReferralRewards
{
    public static async Task ApplyOnFirstPaymentAsync(ApplicationDbContext db, int businessId, DateTime nowUtc, CancellationToken ct)
    {
        var partnerId = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.ReferredByPartnerId).FirstOrDefaultAsync(ct);
        if (partnerId is null) return;
        var partner = await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId && p.IsActive, ct);
        if (partner is null || await db.PartnerCommissions.AnyAsync(c => c.PartnerId == partner.Id && c.BusinessId == businessId, ct)) return;

        // A commission row is the "rewarded once" record, even when the amount is 0 (customers get a month instead).
        db.PartnerCommissions.Add(new PartnerCommission { PartnerId = partner.Id, BusinessId = businessId, Amount = partner.CommissionPerCustomer,
            Status = partner.CommissionPerCustomer > 0 ? CommissionStatus.Owed : CommissionStatus.Paid, PaidAtUtc = partner.CommissionPerCustomer > 0 ? null : nowUtc });

        if (partner.ReferrerBusinessId is int referrer)
        {
            var latest = await db.Subscriptions.Where(s => s.BusinessId == referrer).OrderByDescending(s => s.PeriodEndUtc).FirstOrDefaultAsync(ct);
            if (latest is not null)
            {
                latest.PeriodEndUtc = (latest.PeriodEndUtc > nowUtc ? latest.PeriodEndUtc : nowUtc).AddMonths(1);
                NotificationLog.Add(db, referrer, NotificationType.SubscriptionApproved, "You earned a free month",
                    "A landlord you referred has subscribed. Thank you! Your subscription now runs a month longer.", "/Subscription");
            }
        }
    }
}

public sealed class ReferralService(ApplicationDbContext db, TimeProvider clock) : IReferralService
{
    public async Task<IReadOnlyList<PartnerRow>> ListPartnersAsync(CancellationToken ct = default)
    {
        var partners = await db.Partners.AsNoTracking().OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).ToListAsync(ct);
        return await RowsAsync(partners, ct);
    }

    public async Task<PartnerDetails?> GetPartnerAsync(int partnerId, CancellationToken ct = default)
    {
        var partner = await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == partnerId, ct);
        if (partner is null) return null;
        var row = (await RowsAsync([partner], ct))[0];
        var businesses = await db.Businesses.AsNoTracking().Where(b => b.ReferredByPartnerId == partnerId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new
            {
                b.Id, b.Name, b.CreatedAtUtc,
                Paying = db.SubscriptionPayments.Any(p => p.BusinessId == b.Id && p.Status == PaymentVerificationStatus.Approved),
                Commission = db.PartnerCommissions.Where(c => c.PartnerId == partnerId && c.BusinessId == b.Id)
                    .Select(c => new { c.Id, c.Amount, c.Status, c.PaidAtUtc, c.PaidReference }).FirstOrDefault()
            }).ToListAsync(ct);
        return new PartnerDetails(row, partner.Phone, partner.Email, partner.Notes,
            businesses.Select(b => new ReferredBusiness(b.Id, b.Name, b.CreatedAtUtc, b.Paying, b.Commission?.Id, b.Commission?.Amount,
                b.Commission?.Status, b.Commission?.PaidAtUtc, b.Commission?.PaidReference)).ToList());
    }

    public async Task<Result<int>> CreatePartnerAsync(PartnerInput input, CancellationToken ct = default)
    {
        if (Validate(input) is string error) return Result<int>.Fail(error);
        var code = string.IsNullOrWhiteSpace(input.Code) ? await NewCodeAsync(ct) : Clean(input.Code);
        if (code.Length == 4 && code.All(char.IsDigit)) code = "SN" + code;   // same form people type: 4827 -> SN4827
        if (await db.Partners.AnyAsync(p => p.Code == code, ct)) return Result<int>.Fail($"The code {code} is already used.");
        var partner = new Partner { Code = code };
        Apply(partner, input);
        db.Partners.Add(partner);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(partner.Id);
    }

    public async Task<Result> UpdatePartnerAsync(int partnerId, PartnerInput input, CancellationToken ct = default)
    {
        if (Validate(input) is string error) return Result.Fail(error);
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.Id == partnerId, ct);
        if (partner is null) return Result.Fail("Partner not found.");
        Apply(partner, input);   // the code never changes: links already shared must keep working
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarkCommissionPaidAsync(int commissionId, string reference, CancellationToken ct = default)
    {
        var c = await db.PartnerCommissions.FirstOrDefaultAsync(x => x.Id == commissionId, ct);
        if (c is null) return Result.Fail("Commission not found.");
        if (c.Status == CommissionStatus.Paid) return Result.Fail("Already marked as paid.");
        if (string.IsNullOrWhiteSpace(reference)) return Result.Fail("Enter the payment reference (e.g. the M-PESA code).");
        c.Status = CommissionStatus.Paid;
        c.PaidAtUtc = clock.GetUtcNow().UtcDateTime;
        c.PaidReference = reference.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<bool> AttachAsync(int businessId, string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        var clean = Clean(code);
        if (clean.Length == 4 && clean.All(char.IsDigit)) clean = "SN" + clean;   // "4827" means SN4827
        var partner = await db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Code == clean && p.IsActive, ct);
        if (partner is null || partner.ReferrerBusinessId == businessId) return false;   // no self-referral
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business is null || business.ReferredByPartnerId is not null) return false;   // the first code wins
        business.ReferredByPartnerId = partner.Id;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CustomerReferral> GetCustomerReferralAsync(int businessId, CancellationToken ct = default)
    {
        var partner = await db.Partners.FirstOrDefaultAsync(p => p.ReferrerBusinessId == businessId, ct);
        if (partner is null)
        {
            var name = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.Name).FirstAsync(ct);
            partner = new Partner { Name = name, Kind = PartnerKind.Customer, Code = await NewCodeAsync(ct), ReferrerBusinessId = businessId };
            db.Partners.Add(partner);
            await db.SaveChangesAsync(ct);
        }
        var referred = await db.Businesses.CountAsync(b => b.ReferredByPartnerId == partner.Id, ct);
        var paying = await db.PartnerCommissions.CountAsync(c => c.PartnerId == partner.Id, ct);
        return new CustomerReferral(partner.Code, referred, paying);
    }

    // ---------------------------------------------------------------------

    private async Task<List<PartnerRow>> RowsAsync(List<Partner> partners, CancellationToken ct)
    {
        var ids = partners.Select(p => p.Id).ToList();
        var trials = await db.Businesses.AsNoTracking().Where(b => b.ReferredByPartnerId != null && ids.Contains(b.ReferredByPartnerId.Value))
            .GroupBy(b => b.ReferredByPartnerId!.Value).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var commissions = await db.PartnerCommissions.AsNoTracking().Where(c => ids.Contains(c.PartnerId))
            .Select(c => new { c.PartnerId, c.Amount, c.Status }).ToListAsync(ct);
        var referrerIds = partners.Where(p => p.ReferrerBusinessId != null).Select(p => p.ReferrerBusinessId!.Value).ToList();
        var referrerNames = await db.Businesses.AsNoTracking().Where(b => referrerIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        return partners.Select(p => new PartnerRow(p.Id, p.Name, p.Code, p.Kind, p.CommissionPerCustomer, p.IsActive,
            trials.FirstOrDefault(t => t.Id == p.Id)?.Count ?? 0,
            commissions.Count(c => c.PartnerId == p.Id),
            commissions.Where(c => c.PartnerId == p.Id && c.Status == CommissionStatus.Owed).Sum(c => c.Amount),
            commissions.Where(c => c.PartnerId == p.Id && c.Status == CommissionStatus.Paid).Sum(c => c.Amount),
            p.ReferrerBusinessId is int r ? referrerNames.GetValueOrDefault(r) : null)).ToList();
    }

    private static string? Validate(PartnerInput i)
        => string.IsNullOrWhiteSpace(i.Name) ? "Enter the partner's name."
        : i.CommissionPerCustomer < 0 ? "Commission can't be negative."
        : i.Code is { Length: > 0 } c && (Clean(c).Length < 3 || Clean(c).Length > 20) ? "Codes are 3 to 20 letters and numbers."
        : null;

    private static void Apply(Partner p, PartnerInput i)
    {
        p.Name = i.Name.Trim();
        p.Kind = i.Kind;
        p.Phone = string.IsNullOrWhiteSpace(i.Phone) ? null : i.Phone.Trim();
        p.Email = string.IsNullOrWhiteSpace(i.Email) ? null : i.Email.Trim();
        p.CommissionPerCustomer = i.CommissionPerCustomer;
        p.IsActive = i.IsActive;
        p.Notes = string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim();
    }

    private static string Clean(string code) => new string(code.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>
    /// A new code: "SN" plus 4 random digits (e.g. SN4827). Short and easy to say, branded, and not sequential, so codes
    /// can't be guessed in order and don't reveal how many partners there are. Unique; people may also type just the digits.
    /// </summary>
    private async Task<string> NewCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var code = "SN" + Random.Shared.Next(1000, 10000);
            if (!await db.Partners.AnyAsync(p => p.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("No free SN codes left: use a custom code.");
    }
}
