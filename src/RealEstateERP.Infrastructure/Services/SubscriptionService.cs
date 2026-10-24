using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Depends on: DbContext, ISystemConfigService, IFileStorage, IMemoryCache, TimeProvider.
/// It must NEVER depend on BusinessService, invoicing, credits or any other domain service
/// (BusinessService depends on THIS, one way).
/// </summary>
public sealed class SubscriptionService(
    ApplicationDbContext db,
    ISystemConfigService config,
    IFileStorage storage,
    IMemoryCache cache,
    TimeProvider clock) : ISubscriptionService
{
    private static readonly int[] AllowedMonths = [1, 3, 6, 12];
    private static string SnapshotKey(int businessId) => $"subscription:{businessId}";

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task AddTrialAsync(Business business, CancellationToken ct = default)
    {
        var trialCode = await config.GetAsync(ConfigKeys.TrialPlanCode, ct) ?? "PROFESSIONAL";
        var plan = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Code == trialCode && p.IsActive, ct)
            ?? throw new InvalidOperationException(
                $"Trial plan '{trialCode}' is missing or inactive. Check {ConfigKeys.TrialPlanCode} in Settings.");

        var start = Now;
        db.Subscriptions.Add(new Subscription
        {
            Business = business,          // navigation: works before the business has an Id
            PlanId = plan.Id,
            PeriodStartUtc = start,
            PeriodEndUtc = start.AddDays(plan.TrialDays),
            IsTrial = true
        });

        // FR-15: the trial comes with the plan's monthly credits.
        if (plan.CreditsPerMonth > 0)
            db.CreditTransactions.Add(CreditLedger.Post(business, plan.CreditsPerMonth, CreditTransactionType.Allocation,
                $"{plan.Name} trial credits"));
    }

    public async Task<SubscriptionSnapshot> GetSnapshotAsync(int businessId, CancellationToken ct = default)
        => (await cache.GetOrCreateAsync(SnapshotKey(businessId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await BuildSnapshotAsync(businessId, ct);
        }))!;

    private async Task<SubscriptionSnapshot> BuildSnapshotAsync(int businessId, CancellationToken ct)
    {
        var periods = await db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.BusinessId == businessId)
            .OrderByDescending(s => s.PeriodEndUtc)
            .Take(24)
            .ToListAsync(ct);

        if (periods.Count == 0)
            return SubscriptionSnapshot.None;

        var now = Now;
        var graceDays = await config.GetIntAsync(ConfigKeys.GraceDays, 3, ct);
        var paidThrough = periods[0].PeriodEndUtc;

        // The period covering today (renewals can be queued after the current one), else the latest.
        var current = periods.FirstOrDefault(p => p.PeriodStartUtc <= now && now <= p.PeriodEndUtc) ?? periods[0];

        // Only "trial" if nothing paid is queued after it.
        var isTrial = current.IsTrial && current.PeriodEndUtc >= paidThrough;
        var status = SubscriptionStatusCalculator.Evaluate(isTrial, paidThrough, now, graceDays);

        // Lite: a business whose tariff is free (small rent roll, no instalment sales) stays active after its trial.
        if (status is SubscriptionStatus.Grace or SubscriptionStatus.Suspended)
        {
            var quote = await TariffCalculator.QuoteAsync(db, businessId, await config.GetDecimalAsync(ConfigKeys.AgencyMultiplier, 1.5m, ct), ct);
            if (quote.IsFree && quote.RentBand is not null)
                return new SubscriptionSnapshot(SubscriptionStatus.Active, quote.RentBand.Name, quote.RentBand.Code, null, 0,
                    paidThrough, paidThrough.AddDays(graceDays), false);
        }

        return new SubscriptionSnapshot(
            status,
            current.Plan.Name,
            current.Plan.Code,
            current.Plan.UnitLimit,
            current.Plan.CreditsPerMonth,
            paidThrough,
            paidThrough.AddDays(graceDays),
            isTrial);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetAvailablePlansAsync(int businessId, CancellationToken ct = default)
    {
        var type = await db.Businesses.Where(b => b.Id == businessId).Select(b => (BusinessType?)b.Type).FirstOrDefaultAsync(ct);
        return await db.SubscriptionPlans
            .AsNoTracking()
            .Where(p => p.IsActive && (p.ForBusinessType == null || p.ForBusinessType == type))
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
    }

    public async Task<Result<int>> SubmitRenewalAsync(int businessId, RenewalRequest request, CancellationToken ct = default)
    {
        if (!AllowedMonths.Contains(request.Months))
            return Result<int>.Fail("Choose 1, 3, 6 or 12 months.");

        // Tariffs: the price comes from the business's real rent roll and instalment sales, never from the browser.
        var quote = await TariffCalculator.QuoteAsync(db, businessId, await config.GetDecimalAsync(ConfigKeys.AgencyMultiplier, 1.5m, ct), ct);
        SubscriptionPlan? plan;
        decimal amount;
        if (quote.RentBand is not null || quote.SaleBand is not null)
        {
            if (quote.IsQuoted) return Result<int>.Fail("Your business is above our published bands: contact us for a quote.");
            if (quote.IsFree) return Result<int>.Fail("You're on Lite: there's nothing to pay.");
            plan = await db.SubscriptionPlans.FirstAsync(p => p.Id == quote.PrimaryPlanId, ct);
            amount = quote.TotalFor(request.Months);
        }
        else
        {
            var plans = await GetAvailablePlansAsync(businessId, ct);   // no tariff bands set up: classic plans
            plan = plans.FirstOrDefault(p => p.Id == request.PlanId);
            if (plan is null) return Result<int>.Fail("That plan isn't available for your business.");
            amount = plan.MonthlyFee * request.Months;
        }

        if (await db.SubscriptionPayments.AnyAsync(p => p.BusinessId == businessId && p.Status == PaymentVerificationStatus.Pending, ct))
            return Result<int>.Fail("You already have a payment waiting for verification.");

        var reference = request.Reference.Trim().ToUpperInvariant();
        if (await db.SubscriptionPayments.AnyAsync(p =>
                p.Method == request.Method && p.Reference == reference && p.Status != PaymentVerificationStatus.Rejected, ct))
            return Result<int>.Fail("This transaction reference has already been submitted.");

        if (request.Method == SubscriptionPaymentMethod.BankTransfer && request.Proof is null)
            return Result<int>.Fail("Attach the bank deposit slip or transfer confirmation.");

        string? proofKey = null;
        if (request.Proof is { } proof)
        {
            var maxMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct);
            var error = UploadValidator.Validate(proof.Content, proof.FileName, proof.Length, maxMb);
            if (error is not null) return Result<int>.Fail(error);
            proofKey = await storage.SaveAsync(proof.Content, "subscription-proofs", Path.GetExtension(proof.FileName), ct);
        }

        var payment = new SubscriptionPayment
        {
            BusinessId = businessId,
            PlanId = plan.Id,
            Months = request.Months,
            Amount = amount,
            Method = request.Method,
            Reference = reference,
            ProofStorageKey = proofKey,
            ProofFileName = request.Proof is null ? null : Path.GetFileName(request.Proof.FileName),
            Status = PaymentVerificationStatus.Pending
        };
        db.SubscriptionPayments.Add(payment);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(payment.Id);
    }

    public Task<SubscriptionPayment?> GetPendingPaymentAsync(int businessId, CancellationToken ct = default)
        => db.SubscriptionPayments.AsNoTracking().Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.BusinessId == businessId && p.Status == PaymentVerificationStatus.Pending, ct);

    public async Task<IReadOnlyList<SubscriptionPayment>> GetPaymentsAsync(int businessId, CancellationToken ct = default)
        => await db.SubscriptionPayments.AsNoTracking().Include(p => p.Plan)
            .Where(p => p.BusinessId == businessId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SubscriptionPayment>> GetPendingPaymentsAsync(CancellationToken ct = default)
        => await db.SubscriptionPayments.AsNoTracking().Include(p => p.Plan).Include(p => p.Business)
            .Where(p => p.Status == PaymentVerificationStatus.Pending)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SubscriptionPayment>> GetRecentlyReviewedPaymentsAsync(int take, CancellationToken ct = default)
        => await db.SubscriptionPayments.AsNoTracking().Include(p => p.Plan).Include(p => p.Business)
            .Where(p => p.Status != PaymentVerificationStatus.Pending)
            .OrderByDescending(p => p.ReviewedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<SubscriptionPayment?> GetPaymentAsync(int paymentId, CancellationToken ct = default)
        => db.SubscriptionPayments.AsNoTracking().Include(p => p.Plan).Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<Result> ApproveAsync(int paymentId, string reviewerUserId, string? note, CancellationToken ct = default)
    {
        var payment = await db.SubscriptionPayments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status != PaymentVerificationStatus.Pending)
            return Result.Fail($"This payment was already {payment.Status.ToString().ToLowerInvariant()}.");

        var now = Now;
        var graceDays = await config.GetIntAsync(ConfigKeys.GraceDays, 3, ct);
        var paidThrough = await db.Subscriptions
            .Where(s => s.BusinessId == payment.BusinessId)
            .MaxAsync(s => (DateTime?)s.PeriodEndUtc, ct);

        // Still covered (or within grace)? Continue from the end of the current coverage, so no days are
        // lost or gained. Already suspended? Start today.
        var start = paidThrough is DateTime end && end.AddDays(graceDays) >= now ? end : now;

        db.Subscriptions.Add(new Subscription
        {
            BusinessId = payment.BusinessId,
            PlanId = payment.PlanId,
            PeriodStartUtc = start,
            PeriodEndUtc = start.AddMonths(payment.Months),
            IsTrial = false,
            SourcePaymentId = payment.Id
        });

        payment.Status = PaymentVerificationStatus.Approved;
        payment.ReviewedByUserId = reviewerUserId;
        payment.ReviewedAtUtc = now;
        payment.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        // FR-15: the plan's monthly credits for every month paid, in the same save.
        var plan = await db.SubscriptionPlans.AsNoTracking().FirstAsync(p => p.Id == payment.PlanId, ct);
        NotificationLog.Add(db, payment.BusinessId, NotificationType.SubscriptionApproved, "Subscription payment approved",
            $"{plan.Name}, {payment.Months} month(s). Thank you.", "/Subscription");
        // First payment ever: the partner who referred this business earns their commission (or free month), once.
        if (!await db.SubscriptionPayments.AnyAsync(p => p.BusinessId == payment.BusinessId && p.Status == PaymentVerificationStatus.Approved && p.Id != payment.Id, ct))
            await ReferralRewards.ApplyOnFirstPaymentAsync(db, payment.BusinessId, DateTime.UtcNow, ct);
        if (plan.CreditsPerMonth > 0)
        {
            var business = await db.Businesses.FirstAsync(b => b.Id == payment.BusinessId, ct);
            db.CreditTransactions.Add(CreditLedger.Post(business, plan.CreditsPerMonth * payment.Months, CreditTransactionType.Allocation,
                $"{plan.Name} plan credits, {payment.Months} month(s)", payment.Reference));
        }

        try
        {
            await db.SaveChangesAsync(ct);   // one SaveChanges = one transaction
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Someone else reviewed this payment at the same time. Refresh to see its status.");
        }
        catch (DbUpdateException)
        {
            return Result.Fail("This payment has already been applied.");
        }

        cache.Remove(SnapshotKey(payment.BusinessId));
        return Result.Ok();
    }

    public async Task<Result> RejectAsync(int paymentId, string reviewerUserId, string note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
            return Result.Fail("Give a reason so the business knows what to fix.");

        var payment = await db.SubscriptionPayments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null) return Result.Fail("Payment not found.");
        if (payment.Status != PaymentVerificationStatus.Pending)
            return Result.Fail($"This payment was already {payment.Status.ToString().ToLowerInvariant()}.");

        payment.Status = PaymentVerificationStatus.Rejected;
        payment.ReviewedByUserId = reviewerUserId;
        payment.ReviewedAtUtc = Now;
        payment.ReviewNote = note.Trim();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("Someone else reviewed this payment at the same time. Refresh to see its status.");
        }
        return Result.Ok();
    }
}
