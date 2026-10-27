using RealEstateERP.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Depends on: DbContext, ISystemConfigService (low-level). Nothing else.</summary>
public sealed class PlanService(ApplicationDbContext db, ISystemConfigService config) : IPlanService
{
    public async Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(CancellationToken ct = default)
        => await db.SubscriptionPlans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

    public Task<SubscriptionPlan?> GetAsync(int id, CancellationToken ct = default)
        => db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Result<int>> SaveAsync(SubscriptionPlanInput input, CancellationToken ct = default)
    {
        var code = input.Code.Trim().ToUpperInvariant();

        SubscriptionPlan plan;
        if (input.Id is int id)
        {
            var existing = await db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (existing is null) return Result<int>.Fail("Plan not found.");
            plan = existing;
        }
        else
        {
            if (await db.SubscriptionPlans.AnyAsync(p => p.Code == code, ct))
                return Result<int>.Fail($"A plan with code {code} already exists.");
            plan = new SubscriptionPlan { Code = code };
            db.SubscriptionPlans.Add(plan);
        }

        // Tariff bands: a sensible range that doesn't overlap another active band of the same measure
        // (an overlap would make the price ambiguous; the calculator picks one band per measure).
        if (input.Metric is TariffMetric metric)
        {
            var from = input.BandFrom ?? 0m;
            if (from < 0) return Result<int>.Fail("'From' can't be negative.");
            if (input.BandTo is decimal to && to <= from) return Result<int>.Fail("'To' must be more than 'From' (or blank for no upper limit).");
            if (input.IsActive)
            {
                var others = await db.SubscriptionPlans.AsNoTracking()
                    .Where(p => p.IsActive && p.Metric == metric && p.Id != (input.Id ?? 0)).ToListAsync(ct);
                var clash = others.FirstOrDefault(o => (o.BandFrom ?? 0) <= (input.BandTo ?? decimal.MaxValue) && from <= (o.BandTo ?? decimal.MaxValue));
                if (clash is not null)
                    return Result<int>.Fail($"This range overlaps the active band '{clash.Name}' ({clash.BandFrom:N2} to {(clash.BandTo is decimal t ? t.ToString("N2") : "no limit")}). Adjust or deactivate that band first.");
            }
        }

        var trialCode = await config.GetAsync(ConfigKeys.TrialPlanCode, ct);
        if (!input.IsActive && string.Equals(plan.Code, trialCode, StringComparison.OrdinalIgnoreCase))
            return Result<int>.Fail("This plan is used for new-business trials. Choose another trial plan in Settings first.");

        plan.Name = input.Name.Trim();
        plan.Description = input.Description?.Trim();
        plan.UnitLimit = input.UnitLimit;
        plan.MonthlyFee = input.MonthlyFee;
        plan.CreditsPerMonth = input.CreditsPerMonth;
        plan.TrialDays = input.TrialDays;
        plan.IsActive = input.IsActive;
        plan.SortOrder = input.SortOrder;
        plan.ForBusinessType = input.ForBusinessType;
        plan.Metric = input.Metric;
        plan.BandFrom = input.Metric is null ? null : input.BandFrom ?? 0m;
        plan.BandTo = input.Metric is null ? null : input.BandTo;
        plan.IsQuoted = input.Metric is not null && input.IsQuoted;

        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(plan.Id);
    }
}
