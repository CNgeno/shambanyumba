using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// The one place tariffs are worked out (used by renewals, the subscription status, and the pages), so they always agree.
/// Reads by business id with filters off, because admins and background work quote businesses other than the current one.
/// </summary>
public static class TariffCalculator
{
    public static async Task<List<SubscriptionPlan>> BandsAsync(ApplicationDbContext db, CancellationToken ct)
        => await db.SubscriptionPlans.AsNoTracking().Where(p => p.IsActive && p.Metric != null)
            .OrderBy(p => p.Metric).ThenBy(p => p.BandFrom).ToListAsync(ct);

    public static async Task<TariffQuote> QuoteAsync(ApplicationDbContext db, int businessId, decimal agencyMultiplier, CancellationToken ct)
    {
        var rentRoll = await db.Leases.IgnoreQueryFilters()
            .Where(l => l.BusinessId == businessId && !l.IsDeleted && l.Status == LeaseStatus.Active)
            .SumAsync(l => (decimal?)l.MonthlyRent, ct) ?? 0m;
        var saleAccounts = await db.Sales.IgnoreQueryFilters()
            .CountAsync(s => s.BusinessId == businessId && !s.IsDeleted
                             && (s.Status == SaleStatus.Active || s.Status == SaleStatus.Reserved) && s.AmountPaid < s.Price, ct);
        var isAgency = await db.Businesses.AsNoTracking().AnyAsync(b => b.Id == businessId && b.Type == BusinessType.Agency, ct);
        var bands = await BandsAsync(db, ct);

        TariffBandLine? Line(TariffMetric metric, decimal value, decimal multiplier)
        {
            var band = bands.Where(b => b.Metric == metric)
                .FirstOrDefault(b => value >= (b.BandFrom ?? 0) && (b.BandTo is null || value <= b.BandTo));
            return band is null ? null : new TariffBandLine(band.Id, band.Code, band.Name, band.BandFrom ?? 0, band.BandTo,
                Math.Round(band.MonthlyFee * multiplier, 0), band.IsQuoted, band.CreditsPerMonth);
        }

        return new TariffQuote(rentRoll, saleAccounts, isAgency, agencyMultiplier,
            Line(TariffMetric.RentRoll, rentRoll, isAgency ? agencyMultiplier : 1m),
            saleAccounts > 0 ? Line(TariffMetric.SaleAccounts, saleAccounts, 1m) : null);
    }
}

public sealed class TariffService(ApplicationDbContext db, ISystemConfigService config) : ITariffService
{
    public async Task<TariffQuote> QuoteAsync(int businessId, CancellationToken ct = default)
        => await TariffCalculator.QuoteAsync(db, businessId, await MultiplierAsync(ct), ct);

    public async Task<TariffTable> GetTableAsync(CancellationToken ct = default)
    {
        var bands = await TariffCalculator.BandsAsync(db, ct);
        TariffBandLine Line(SubscriptionPlan b) => new(b.Id, b.Code, b.Name, b.BandFrom ?? 0, b.BandTo, b.MonthlyFee, b.IsQuoted, b.CreditsPerMonth);
        var trialCode = await config.GetAsync(ConfigKeys.TrialPlanCode, ct);
        var trialDays = await db.SubscriptionPlans.AsNoTracking().Where(p => p.Code == trialCode).Select(p => (int?)p.TrialDays).FirstOrDefaultAsync(ct) ?? 60;
        return new TariffTable(bands.Where(b => b.Metric == TariffMetric.RentRoll).Select(Line).ToList(),
            bands.Where(b => b.Metric == TariffMetric.SaleAccounts).Select(Line).ToList(), await MultiplierAsync(ct), trialDays);
    }

    private async Task<decimal> MultiplierAsync(CancellationToken ct) => await config.GetDecimalAsync(ConfigKeys.AgencyMultiplier, 1.5m, ct);
}
