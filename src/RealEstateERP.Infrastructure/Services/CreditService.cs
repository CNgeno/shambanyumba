using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>LOW-LEVEL (FR-15). Depends on: DbContext, ICurrentUserService, ISystemConfigService, TimeProvider.</summary>
public sealed class CreditService(
    ApplicationDbContext db,
    ICurrentUserService currentUser,
    ISystemConfigService config,
    TimeProvider clock) : ICreditService
{
    private const int MaxAttempts = 3;
    private int BusinessId => currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");

    public async Task<int> GetBalanceAsync(CancellationToken ct = default)
        => await db.Businesses.Where(b => b.Id == BusinessId).Select(b => b.CreditBalance).FirstOrDefaultAsync(ct);

    public async Task<CreditOverview> GetOverviewAsync(CancellationToken ct = default)
    {
        var businessId = BusinessId;
        return new CreditOverview(
            await GetBalanceAsync(ct),
            await db.CreditTransactions.AsNoTracking().Where(t => t.BusinessId == businessId)
                .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id).Take(100).ToListAsync(ct),
            await db.CreditPurchases.AsNoTracking().Where(p => p.BusinessId == businessId)
                .OrderByDescending(p => p.CreatedAtUtc).Take(20).ToListAsync(ct),
            await config.GetDecimalAsync(ConfigKeys.CreditPrice, 5m, ct),
            await BundlesAsync(ct));
    }

    /// <summary>Deducts credits atomically (rowversion + retry). Refuses if the balance is too low.</summary>
    public async Task<Result> TryConsumeAsync(int credits, string description, string? reference, CancellationToken ct = default)
    {
        if (credits <= 0) return Result.Ok();
        if (db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("TryConsumeAsync must run before other changes are added to the context.");

        for (var attempt = 1; ; attempt++)
        {
            var business = await db.Businesses.FirstAsync(b => b.Id == BusinessId, ct);
            if (business.CreditBalance < credits)
                return Result.Fail($"This needs {credits} credit{(credits == 1 ? "" : "s")} and you have {business.CreditBalance}. Buy more credits to continue.");

            db.CreditTransactions.Add(CreditLedger.Post(business, -credits, CreditTransactionType.Usage, description, reference));
            try
            {
                await db.SaveChangesAsync(ct);
                return Result.Ok();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                db.ChangeTracker.Clear();   // someone else spent credits at the same moment: re-read and retry
            }
        }
    }

    public async Task<Result<int>> RequestPurchaseAsync(CreditPurchaseInput input, CancellationToken ct = default)
    {
        var bundles = await BundlesAsync(ct);
        if (!bundles.Contains(input.Credits)) return Result<int>.Fail("Choose one of the credit bundles.");

        var reference = input.Reference.Trim().ToUpperInvariant();
        if (reference.Length < 6) return Result<int>.Fail("Enter the transaction reference.");
        if (await db.CreditPurchases.AnyAsync(p => p.Method == input.Method && p.Reference == reference && p.Status != PaymentVerificationStatus.Rejected, ct))
            return Result<int>.Fail("This reference has already been submitted.");

        var price = await config.GetDecimalAsync(ConfigKeys.CreditPrice, 5m, ct);
        var purchase = new CreditPurchase
        {
            BusinessId = BusinessId, Credits = input.Credits, Amount = input.Credits * price, Method = input.Method, Reference = reference
        };
        db.CreditPurchases.Add(purchase);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(purchase.Id);
    }

    // ------------------------------------------------------------------ platform admin

    public async Task<IReadOnlyList<CreditPurchase>> GetPendingPurchasesAsync(CancellationToken ct = default)
        => await db.CreditPurchases.AsNoTracking().Include(p => p.Business)
            .Where(p => p.Status == PaymentVerificationStatus.Pending)
            .OrderBy(p => p.CreatedAtUtc).ToListAsync(ct);

    public async Task<Result> ApprovePurchaseAsync(int purchaseId, string reviewerUserId, CancellationToken ct = default)
    {
        var purchase = await db.CreditPurchases.Include(p => p.Business).FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
        if (purchase is null) return Result.Fail("Purchase not found.");
        if (purchase.Status != PaymentVerificationStatus.Pending) return Result.Fail("This purchase was already reviewed.");

        purchase.Status = PaymentVerificationStatus.Approved;
        purchase.ReviewedByUserId = reviewerUserId;
        purchase.ReviewedAtUtc = clock.GetUtcNow().UtcDateTime;
        NotificationLog.Add(db, purchase.BusinessId, NotificationType.CreditsApproved, $"{purchase.Credits} credits added", $"Your purchase ({purchase.Reference}) was approved.", "/Credits");
        db.CreditTransactions.Add(CreditLedger.Post(purchase.Business, purchase.Credits, CreditTransactionType.Purchase,
            $"{purchase.Credits} credits bought", purchase.Reference));
        return await SaveReviewAsync(ct);
    }

    public async Task<Result> RejectPurchaseAsync(int purchaseId, string reviewerUserId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason so the business knows what to fix.");
        var purchase = await db.CreditPurchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
        if (purchase is null) return Result.Fail("Purchase not found.");
        if (purchase.Status != PaymentVerificationStatus.Pending) return Result.Fail("This purchase was already reviewed.");

        purchase.Status = PaymentVerificationStatus.Rejected;
        purchase.ReviewedByUserId = reviewerUserId;
        purchase.ReviewedAtUtc = clock.GetUtcNow().UtcDateTime;
        purchase.ReviewNote = reason.Trim();
        return await SaveReviewAsync(ct);
    }

    public async Task<Result> AdjustAsync(int businessId, int credits, string reason, CancellationToken ct = default)
    {
        if (credits == 0) return Result.Fail("Enter a number of credits (negative to remove).");
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for the adjustment.");
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business is null) return Result.Fail("Business not found.");
        if (business.CreditBalance + credits < 0) return Result.Fail($"The balance can't go below zero (it is {business.CreditBalance}).");

        db.CreditTransactions.Add(CreditLedger.Post(business, credits, CreditTransactionType.Adjustment, reason.Trim()));
        return await SaveReviewAsync(ct);
    }

    // ---------------------------------------------------------------------

    private async Task<IReadOnlyList<int>> BundlesAsync(CancellationToken ct)
        => ((await config.GetAsync(ConfigKeys.CreditBundles, ct)) ?? "100,250,500,1000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .Where(n => n > 0).Distinct().OrderBy(n => n).ToList();

    private async Task<Result> SaveReviewAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("This was changed by someone else at the same moment. Refresh and try again.");
        }
    }
}
