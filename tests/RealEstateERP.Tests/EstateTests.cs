using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class EstateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int AssetId, int UnitId, int LeaseId, int GarbageId, int WaterId)
    {
        public SystemConfigService Config() => new(Db, new MemoryCache(new MemoryCacheOptions()));
        public EstateService Estate() => new(Db, new FakeStorage(), Config());
        public BillingRunService Billing() => new(Db, new NumberSequenceService(Db), new CreditService(Db, User, Config(), Clock), Clock);
        public ListingService Listings() => new(Db, new FakeStorage(), Config(), User, Clock);
    }

    private static async Task<Ctx> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var asset = new Asset { Name = "Bethlehem Estate", Type = AssetType.Estate, Location = "Kitengela" };
        var let = new Unit { Asset = asset, UnitNumber = "H1", Type = UnitType.Apartment, Bedrooms = 1, MonthlyRent = 6_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Mary Atieno" };
        var lease = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = let, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 8, 1), MonthlyRent = 6_000m, RentDueDay = 5 };
        var garbage = new ChargeDefinition { Name = "Garbage", Type = ChargeType.Recurring, DefaultAmount = 120m };
        var water = new ChargeDefinition { Name = "Water", Type = ChargeType.Utility, UnitRate = 170m, UnitOfMeasure = "unit" };
        db.AddRange(asset, let, tenant, lease, garbage, water);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), asset.Id, let.Id, lease.Id, garbage.Id, water.Id);
    }

    [Fact]
    public async Task Estate_charges_are_applied_to_leases_that_lack_them_with_the_estates_amount()
    {
        var c = await SetupAsync();
        Assert.True((await c.Estate().AddChargeAsync(c.AssetId, new PropertyChargeInput(c.GarbageId, 150m, null))).Succeeded);   // this estate charges 150
        Assert.False((await c.Estate().AddChargeAsync(c.AssetId, new PropertyChargeInput(c.GarbageId, 150m, null))).Succeeded);  // only once

        Assert.Equal(1, Assert.Single(await c.Estate().GetChargesAsync(c.AssetId)).LeasesMissing);
        Assert.Equal(1, (await c.Estate().ApplyChargesToLeasesAsync(c.AssetId)).Value);
        Assert.Equal(0, (await c.Estate().ApplyChargesToLeasesAsync(c.AssetId)).Value);   // nothing left to add
        var onLease = await c.Db.LeaseCharges.SingleAsync(x => x.LeaseId == c.LeaseId);
        Assert.Equal(150m, onLease.AmountOverride);
    }

    [Fact]
    public async Task Invoice_preview_warns_about_a_missing_charge_and_a_missing_reading_until_fixed()
    {
        var c = await SetupAsync();
        await c.Estate().AddChargeAsync(c.AssetId, new PropertyChargeInput(c.GarbageId, null, null));
        await c.Estate().AddChargeAsync(c.AssetId, new PropertyChargeInput(c.WaterId, null, null));
        var request = new BillingRunRequest(2026, 10, null, false, false);

        var row = Assert.Single(await c.Billing().PreviewAsync(request));
        Assert.Equal(2, row.Warnings!.Count);
        Assert.Contains(row.Warnings, w => w.IsMissingCharge && w.Message.StartsWith("Garbage"));
        Assert.Contains(row.Warnings, w => !w.IsMissingCharge && w.Message.Contains("water"));

        await c.Estate().ApplyChargesToLeasesAsync(c.AssetId);
        c.Db.UtilityMeterReadings.Add(new UtilityMeterReading { UnitId = c.UnitId, ChargeDefinitionId = c.WaterId, ReadingDate = new DateOnly(2026, 9, 30),
            PreviousReading = 100, CurrentReading = 112, Consumption = 12, UnitRate = 170, Amount = 2_040 });
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        var fixedRow = Assert.Single(await c.Billing().PreviewAsync(request));
        Assert.Null(fixedRow.Warnings);
        Assert.Equal(6_000m + 120m + 2_040m, fixedRow.EstimatedTotal);
    }

    [Fact]
    public async Task Vacant_rental_units_are_listed_once_with_estate_photos_and_plots_for_sale_are_skipped()
    {
        var c = await SetupAsync();
        c.Db.Units.AddRange(
            new Unit { AssetId = c.AssetId, UnitNumber = "H2", Type = UnitType.Apartment, Bedrooms = 1, MonthlyRent = 6_000m, Status = UnitStatus.Available },
            new Unit { AssetId = c.AssetId, UnitNumber = "P1", Type = UnitType.Plot, SalePrice = 1_000_000m, Status = UnitStatus.Available });
        c.Db.AssetPhotos.Add(new AssetPhoto { AssetId = c.AssetId, StorageKey = "estate-photos/gate.jpg", FileName = "gate.jpg" });
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        Assert.Equal(1, await c.Listings().AutoListVacantAsync());   // H2 only (H1 is let, P1 is for sale)
        var listing = await c.Db.Listings.Include(l => l.Unit).SingleAsync();
        Assert.Equal(("H2", ListingStatus.Published, 6_000m), (listing.Unit.UnitNumber, listing.Status, listing.Price));
        Assert.Equal("estate-photos/gate.jpg", (await c.Db.ListingPhotos.SingleAsync()).StorageKey);

        c.Db.ChangeTracker.Clear();
        Assert.Equal(0, await c.Listings().AutoListVacantAsync());   // already listed
    }

    [Fact]
    public async Task Setup_steps_follow_the_estate()
    {
        var c = await SetupAsync();
        var before = await c.Estate().GetSetupAsync(c.AssetId);
        Assert.Equal("charges", before.Next!.Key);   // units and a lease exist; charges are the first gap

        await c.Estate().AddChargeAsync(c.AssetId, new PropertyChargeInput(c.WaterId, null, null));
        var after = await c.Estate().GetSetupAsync(c.AssetId);
        Assert.Contains(after.Steps, s => s.Key == "readings" && !s.Done);   // a metered charge adds the readings step
        Assert.Equal("readings", after.Next!.Key);
    }
}
