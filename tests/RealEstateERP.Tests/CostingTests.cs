using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class CostingRulesTests
{
    [Fact]
    public void Allocates_by_size_and_always_adds_up()
    {
        var (shares, used) = CostingRules.Allocate(1_000_000m,
            [new AllocationUnit(1, 400, null), new AllocationUnit(2, 400, null), new AllocationUnit(3, 200, null)], CostAllocationMethod.BySize);
        Assert.Equal(CostAllocationMethod.BySize, used);
        Assert.Equal((400_000m, 400_000m, 200_000m), (shares[1], shares[2], shares[3]));

        var (thirds, _) = CostingRules.Allocate(100m, [new AllocationUnit(1, null, null), new AllocationUnit(2, null, null), new AllocationUnit(3, null, null)], CostAllocationMethod.Equal);
        Assert.Equal(100m, thirds.Values.Sum());   // 33.34 + 33.33 + 33.33
    }

    [Fact]
    public void Falls_back_to_equal_when_the_basis_is_missing()
    {
        var (shares, used) = CostingRules.Allocate(900m, [new AllocationUnit(1, 450, 100), new AllocationUnit(2, null, 200), new AllocationUnit(3, 450, 300)], CostAllocationMethod.BySize);
        Assert.Equal(CostAllocationMethod.Equal, used);
        Assert.All(shares.Values, v => Assert.Equal(300m, v));

        var (byValue, usedValue) = CostingRules.Allocate(600m, [new AllocationUnit(1, null, 100), new AllocationUnit(2, null, 200)], CostAllocationMethod.ByValue);
        Assert.Equal(CostAllocationMethod.ByValue, usedValue);
        Assert.Equal((200m, 400m), (byValue[1], byValue[2]));
    }

    [Fact]
    public void Expected_cost_break_even_and_yield()
    {
        Assert.Equal(600m, CostingRules.Expected(500m, 600m));   // overspend counts
        Assert.Equal(500m, CostingRules.Expected(500m, 200m));   // unspent budget still expected

        Assert.Equal(0, CostingRules.UnitsToBreakEven(1_000m, 1_200m, [500m]));
        Assert.Equal(2, CostingRules.UnitsToBreakEven(1_650m, 900m, [800m, 500m]));   // cheapest first: 500 then 800
        Assert.Null(CostingRules.UnitsToBreakEven(5_000m, 0m, [1_000m, 1_000m]));

        Assert.Equal(12.0m, CostingRules.YieldPercent(120_000m, 1_000_000m));
        Assert.Equal(84, CostingRules.PaybackMonths(1_000_000m, 12_000m));
        Assert.Equal(CostGroup.Development, CostingRules.Group(CostCategory.Infrastructure));
        Assert.False(CostingRules.IsDevelopmentCost(CostCategory.Letting));
    }
}

public class ProjectCostServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static ProjectCostService Service(TestDbContext db)
        => new(db, new FakeStorage(), new SystemConfigService(db, new MemoryCache(new MemoryCacheOptions())), new FakeClock(Now));

    /// <summary>Land 1M; plots 400/400/200 sqm; Plot 1 sold for 900k; Plots 2 and 3 asking 800k and 500k.</summary>
    private static async Task<(TestDbContext Db, int AssetId, int[] Plots, int OtherUnit)> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var asset = new Asset { Name = "Savanna Heights", Type = AssetType.Land, AcquisitionCost = 1_000_000m, CostAllocation = CostAllocationMethod.BySize };
        var other = new Asset { Name = "Elsewhere", Type = AssetType.Building };
        var p1 = new Unit { Asset = asset, UnitNumber = "Plot 1", Type = UnitType.Plot, SizeSqm = 400, SalePrice = 850_000m, Status = UnitStatus.Sold };
        var p2 = new Unit { Asset = asset, UnitNumber = "Plot 2", Type = UnitType.Plot, SizeSqm = 400, SalePrice = 800_000m };
        var p3 = new Unit { Asset = asset, UnitNumber = "Plot 3", Type = UnitType.Plot, SizeSqm = 200, SalePrice = 500_000m };
        var otherUnit = new Unit { Asset = other, UnitNumber = "X1", Type = UnitType.Apartment };
        var buyer = new Buyer { FullName = "Ruth Achieng" };
        db.AddRange(asset, other, p1, p2, p3, otherUnit, buyer,
            new Sale { SaleNumber = "SL-1", Buyer = buyer, Unit = p1, SaleDate = Today.AddMonths(-1), Price = 900_000m, AmountPaid = 300_000m, Status = SaleStatus.Active });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (db, asset.Id, [p1.Id, p2.Id, p3.Id], otherUnit.Id);
    }

    private static CostInput Cost(int assetId, CostKind kind, CostCategory category, decimal amount, int? unitId = null, DateOnly? date = null)
        => new(assetId, unitId, kind, category, $"{category} {kind}", amount, kind == CostKind.Actual ? date ?? Today.AddDays(-5) : date, null, null, null);

    [Fact]
    public async Task Overview_spreads_costs_and_works_out_margins()
    {
        var (db, assetId, plots, _) = await SetupAsync();
        var svc = Service(db);
        await svc.SaveCostAsync(null, Cost(assetId, CostKind.Budget, CostCategory.Infrastructure, 500_000m), null);
        await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Infrastructure, 600_000m), null);   // over budget
        await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Construction, 50_000m, unitId: plots[2]), null);   // Plot 3 only
        await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Letting, 99_000m), null);   // not development cost

        var o = (await svc.GetOverviewAsync(assetId))!;
        Assert.Equal(1_650_000m, o.ExpectedTotal);   // land 1M + infra 600k + plot-3 work 50k
        Assert.Equal(1_500_000m, o.BudgetTotal);     // land 1M + infra budget 500k (the plot-3 work had no budget)
        var infra = o.Categories.Single(c => c.Category == CostCategory.Infrastructure);
        Assert.Equal(100_000m, infra.Variance);

        var byNumber = o.Units.ToDictionary(u => u.UnitNumber);
        Assert.Equal(640_000m, byNumber["Plot 1"].Cost);                         // 1.6M shared x 400/1000
        Assert.Equal((320_000m, 50_000m), (byNumber["Plot 3"].SharedCost, byNumber["Plot 3"].DirectCost));
        Assert.Equal(260_000m, byNumber["Plot 1"].Margin);                       // sold for 900k
        Assert.True(byNumber["Plot 1"].Contracted);

        Assert.Equal((900_000m, 1_300_000m, 300_000m), (o.Sales.ContractedRevenue, o.Sales.UnsoldValue, o.Sales.Collected));
        Assert.Equal(550_000m, o.Sales.ProjectedMargin);                         // 2.2M - 1.65M
        Assert.Equal(2, o.Sales.UnitsToBreakEven);
    }

    [Fact]
    public async Task Actual_costs_need_a_past_date_and_a_unit_of_this_property()
    {
        var (db, assetId, _, otherUnit) = await SetupAsync();
        var svc = Service(db);
        Assert.False((await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Marketing, 1_000m) with { Date = null }, null)).Succeeded);
        Assert.False((await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Marketing, 1_000m, date: Today.AddDays(3)), null)).Succeeded);
        Assert.False((await svc.SaveCostAsync(null, Cost(assetId, CostKind.Actual, CostCategory.Marketing, 1_000m, unitId: otherUnit), null)).Succeeded);
        Assert.True((await svc.SaveCostAsync(null, Cost(assetId, CostKind.Budget, CostCategory.Marketing, 1_000m, date: Today.AddMonths(6)), null)).Succeeded);   // budgets may be future
    }

    [Fact]
    public async Task Budgeted_land_price_is_used_until_the_land_is_bought()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var asset = new Asset { Name = "Future project", Type = AssetType.Land, EstimatedLandCost = 5_000_000m };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var o = (await Service(db).GetOverviewAsync(asset.Id))!;
        var land = o.Categories.Single(c => c.Category is null);
        Assert.Equal((5_000_000m, 0m, 5_000_000m), (land.Budget, land.Actual, land.Expected));
    }
}
