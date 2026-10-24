using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class TariffTests
{
    private static SubscriptionPlan Band(string code, TariffMetric m, decimal from, decimal? to, decimal fee, bool quoted = false)
        => new() { Code = code, Name = code, Metric = m, BandFrom = from, BandTo = to, MonthlyFee = fee, IsQuoted = quoted, IsActive = true, CreditsPerMonth = 50 };

    private static async Task<(TestDbContext Db, int BusinessId)> SetupAsync(decimal[] rents, int saleAccounts = 0, BusinessType type = BusinessType.Owner)
    {
        var (db, user) = TestDbContext.Create();
        db.SubscriptionPlans.AddRange(
            Band("LITE", TariffMetric.RentRoll, 0, 20_000, 0), Band("RENT-1", TariffMetric.RentRoll, 20_000.01m, 50_000, 600),
            Band("RENT-2", TariffMetric.RentRoll, 50_000.01m, 100_000, 1_200), Band("RENT-X", TariffMetric.RentRoll, 100_000.01m, null, 0, quoted: true),
            Band("SALE-1", TariffMetric.SaleAccounts, 1, 10, 1_000));
        var business = new Business { Name = "Bethlehem Estate", Type = type };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;

        var asset = new Asset { Name = "Estate", Type = AssetType.Estate };
        var tenant = new Tenant { FullName = "T" };
        db.AddRange(asset, tenant);
        var i = 0;
        foreach (var rent in rents)
        {
            var unit = new Unit { Asset = asset, UnitNumber = $"H{++i}", MonthlyRent = rent, Status = UnitStatus.Occupied };
            db.Leases.Add(new Lease { LeaseNumber = $"L-{i}", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = rent, RentDueDay = 5 });
        }
        for (var s = 0; s < saleAccounts; s++)
        {
            var plot = new Unit { Asset = asset, UnitNumber = $"P{s}", Type = UnitType.Plot, SalePrice = 1_000_000, Status = UnitStatus.Reserved };
            db.Sales.Add(new Sale { SaleNumber = $"SL-{s}", Buyer = new Buyer { FullName = "B" }, Unit = plot, SaleDate = new DateOnly(2026, 6, 1),
                Price = 1_000_000, AmountPaid = 100_000, Status = SaleStatus.Active, PaymentPlan = PaymentPlan.Instalments });
        }
        await db.SaveChangesAsync();
        return (db, business.Id);
    }

    [Theory]
    [InlineData(new double[] { 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000, 2000 }, 0, true)]     // 10 x 2,000 = 20,000: Lite
    [InlineData(new double[] { 6000, 6000, 6000, 6000, 6000, 6000, 6000 }, 600, false)]                      // 7 x 6,000 = 42,000
    [InlineData(new double[] { 7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000, 7000 }, 1200, false)]   // 10 x 7,000 = 70,000
    public async Task Rent_roll_decides_the_band(double[] rents, double fee, bool free)
    {
        var (db, id) = await SetupAsync(rents.Select(r => (decimal)r).ToArray());
        var q = await TariffCalculator.QuoteAsync(db, id, 1.5m, default);
        Assert.Equal((decimal)fee, q.MonthlyTotal);
        Assert.Equal(free, q.IsFree);
    }

    [Fact]
    public async Task Agencies_pay_the_premium_and_sellers_add_their_band()
    {
        var (agencyDb, agency) = await SetupAsync(Enumerable.Repeat(6_000m, 7).ToArray(), type: BusinessType.Agency);
        Assert.Equal(900m, (await TariffCalculator.QuoteAsync(agencyDb, agency, 1.5m, default)).MonthlyTotal);   // 600 x 1.5

        var (bothDb, both) = await SetupAsync(Enumerable.Repeat(6_000m, 7).ToArray(), saleAccounts: 3);
        var q = await TariffCalculator.QuoteAsync(bothDb, both, 1.5m, default);
        Assert.Equal(1_600m, q.MonthlyTotal);   // rent band 600 + sales band 1,000
        Assert.Equal(16_000m, q.TotalFor(12)); // yearly: 10 months charged
    }

    [Fact]
    public async Task Above_the_bands_is_quoted()
    {
        var (db, id) = await SetupAsync(Enumerable.Repeat(60_000m, 2).ToArray());   // 120,000
        Assert.True((await TariffCalculator.QuoteAsync(db, id, 1.5m, default)).IsQuoted);
    }

    [Fact]
    public async Task Renewal_amount_comes_from_the_tariff_not_the_browser()
    {
        var (db, id) = await SetupAsync(Enumerable.Repeat(7_000m, 10).ToArray());   // 70,000: KES 1,200
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new SubscriptionService(db, new SystemConfigService(db, cache), new FakeStorage(), cache, new FakeClock(DateTimeOffset.UtcNow));
        var cheapest = db.SubscriptionPlans.Single(p => p.Code == "LITE").Id;
        var result = await service.SubmitRenewalAsync(id, new RenewalRequest(cheapest, 3, SubscriptionPaymentMethod.Mpesa, "TAR1F2E3G4", null));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3_600m, db.SubscriptionPayments.Single().Amount);   // 1,200 x 3, whatever plan the browser sent
    }

    [Fact]
    public async Task Lite_stays_active_after_the_trial()
    {
        var (db, id) = await SetupAsync([5_000m, 5_000m]);   // 10,000: Lite
        var trial = new SubscriptionPlan { Code = "TRIAL", Name = "Trial", MonthlyFee = 0, IsActive = true };
        db.SubscriptionPlans.Add(trial);
        db.Subscriptions.Add(new Subscription { BusinessId = id, Plan = trial, IsTrial = true, PeriodStartUtc = DateTime.UtcNow.AddDays(-90), PeriodEndUtc = DateTime.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = await new SubscriptionService(db, new SystemConfigService(db, cache), new FakeStorage(), cache, new FakeClock(DateTimeOffset.UtcNow)).GetSnapshotAsync(id);
        Assert.Equal(SubscriptionStatus.Active, snapshot.Status);
        Assert.Equal("LITE", snapshot.PlanCode);
    }
}
