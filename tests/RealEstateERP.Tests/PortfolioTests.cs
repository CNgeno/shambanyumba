using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

/// <summary>Only the snapshot matters to UnitService.</summary>
public sealed class FakeSubscriptions(int? unitLimit, string planName = "Starter") : ISubscriptionService
{
    public Task<SubscriptionSnapshot> GetSnapshotAsync(int businessId, CancellationToken ct = default)
        => Task.FromResult(new SubscriptionSnapshot(SubscriptionStatus.Active, planName, planName.ToUpperInvariant(),
            unitLimit, 50, DateTime.UtcNow.AddDays(20), DateTime.UtcNow.AddDays(23), false));

    public Task AddTrialAsync(Business business, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<SubscriptionPlan>> GetAvailablePlansAsync(int businessId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Result<int>> SubmitRenewalAsync(int businessId, RenewalRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionPayment?> GetPendingPaymentAsync(int businessId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<SubscriptionPayment>> GetPaymentsAsync(int businessId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<SubscriptionPayment>> GetPendingPaymentsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<SubscriptionPayment>> GetRecentlyReviewedPaymentsAsync(int take, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<SubscriptionPayment?> GetPaymentAsync(int paymentId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Result> ApproveAsync(int paymentId, string reviewerUserId, string? note, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Result> RejectAsync(int paymentId, string reviewerUserId, string note, CancellationToken ct = default) => throw new NotSupportedException();
}

public class StructureTreeTests
{
    // Block A (1) > Floor 1 (2) > Wing X (3);  Block B (4)
    private static readonly StructureNodeDto[] Nodes =
    [
        new(1, null, "Block A", StructureNodeType.Block, 1),
        new(2, 1, "Floor 1", StructureNodeType.Floor, 0),
        new(3, 2, "Wing X", StructureNodeType.Wing, 0),
        new(4, null, "Block B", StructureNodeType.Block, 2)
    ];

    [Fact]
    public void Flatten_is_depth_first_with_depth_and_paths()
    {
        var rows = StructureTree.Flatten(Nodes);
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(r => r.Id));
        Assert.Equal(new[] { 0, 1, 2, 0 }, rows.Select(r => r.Depth));
        Assert.Equal("Block A › Floor 1 › Wing X", rows[2].Path);
    }

    [Theory]
    [InlineData(1, 1, true)]    // itself
    [InlineData(1, 3, true)]    // grandchild
    [InlineData(2, 1, false)]   // parent is not a descendant
    [InlineData(1, 4, false)]   // sibling
    public void IsSelfOrDescendant(int nodeId, int candidate, bool expected)
        => Assert.Equal(expected, StructureTree.IsSelfOrDescendant(Nodes, nodeId, candidate));

    [Fact]
    public void Flatten_survives_a_corrupt_cycle()
    {
        StructureNodeDto[] cyclic = [new(1, 2, "A", StructureNodeType.Block, 0), new(2, 1, "B", StructureNodeType.Block, 0)];
        Assert.Empty(StructureTree.Flatten(cyclic));   // no root, no infinite loop
        Assert.NotNull(StructureTree.Paths(cyclic));
    }
}

public class NaturalComparerTests
{
    [Fact]
    public void Sorts_numbers_by_value()
    {
        string[] input = ["A10", "A2", "a1", "B1", "A02x", "Plot 12", "Plot 3"];
        var sorted = input.OrderBy(x => x, NaturalComparer.Instance).ToList();
        Assert.Equal(new[] { "a1", "A2", "A02x", "A10", "B1", "Plot 3", "Plot 12" }, sorted);
    }
}

public class UnitStatusRulesTests
{
    [Theory]
    [InlineData(UnitStatus.Available, UnitStatus.Reserved, true)]
    [InlineData(UnitStatus.Reserved, UnitStatus.Available, true)]
    [InlineData(UnitStatus.Available, UnitStatus.Inactive, true)]
    [InlineData(UnitStatus.Available, UnitStatus.Sold, true)]
    [InlineData(UnitStatus.Available, UnitStatus.Occupied, false)]   // only via a lease
    [InlineData(UnitStatus.Occupied, UnitStatus.Available, false)]   // only by ending the lease
    [InlineData(UnitStatus.Sold, UnitStatus.Available, false)]
    public void Manual_changes(UnitStatus from, UnitStatus to, bool allowed)
        => Assert.Equal(allowed, UnitStatusRules.ManualChangeError(from, to) is null);
}

public class PortfolioServiceTests
{
    private static async Task<(TestDbContext Db, FakeCurrentUser User, int AssetId)> SetupAsync(int businessId = 1)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = businessId;
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Estate };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        return (db, user, asset.Id);
    }

    private static UnitInput Unit(int assetId, string number, int? nodeId = null)
        => new(assetId, nodeId, number, null, UnitType.Apartment, null, 2, 1, 25_000m, null, null, null, null);

    [Fact]
    public async Task Plan_limit_blocks_new_units()
    {
        var (db, user, assetId) = await SetupAsync();
        var units = new UnitService(db, new FakeSubscriptions(unitLimit: 3), user);

        Assert.True((await units.BulkCreateAsync(new BulkUnitInput(assetId, null, "A", 1, 3, null, UnitType.Apartment, null, null, null, 10_000m))).Succeeded);
        var fourth = await units.CreateAsync(Unit(assetId, "A4"));

        Assert.False(fourth.Succeeded);
        Assert.Contains("Starter", fourth.Error);
    }

    [Fact]
    public async Task Inactive_units_free_up_capacity_and_reactivating_is_checked()
    {
        var (db, user, assetId) = await SetupAsync();
        var units = new UnitService(db, new FakeSubscriptions(unitLimit: 2), user);
        var first = (await units.CreateAsync(Unit(assetId, "1"))).Value;
        await units.CreateAsync(Unit(assetId, "2"));

        Assert.True((await units.ChangeStatusAsync(first, UnitStatus.Inactive)).Succeeded);
        Assert.True((await units.CreateAsync(Unit(assetId, "3"))).Succeeded);       // room again
        Assert.False((await units.ChangeStatusAsync(first, UnitStatus.Available)).Succeeded);   // would exceed
    }

    [Fact]
    public async Task Bulk_create_numbers_units_and_rejects_duplicates()
    {
        var (db, user, assetId) = await SetupAsync();
        var units = new UnitService(db, new FakeSubscriptions(unitLimit: null), user);

        var ok = await units.BulkCreateAsync(new BulkUnitInput(assetId, null, "A", 101, 10, 1, UnitType.Apartment, null, 2, 1, 25_000m));
        Assert.Equal(10, ok.Value);
        Assert.Equal(10, await db.Units.CountAsync(u => u.UnitNumber.StartsWith("A1")));

        var clash = await units.BulkCreateAsync(new BulkUnitInput(assetId, null, "A", 109, 5, 1, UnitType.Apartment, null, 2, 1, 25_000m));
        Assert.False(clash.Succeeded);
        Assert.Contains("A109", clash.Error);
        Assert.Equal(10, await db.Units.CountAsync());   // nothing partially created
    }

    [Fact]
    public async Task Occupied_cannot_be_set_by_hand()
    {
        var (db, user, assetId) = await SetupAsync();
        var units = new UnitService(db, new FakeSubscriptions(unitLimit: null), user);
        var id = (await units.CreateAsync(Unit(assetId, "1"))).Value;

        Assert.False((await units.ChangeStatusAsync(id, UnitStatus.Occupied)).Succeeded);
    }

    [Fact]
    public async Task A_node_cannot_be_moved_inside_its_own_child()
    {
        var (db, user, assetId) = await SetupAsync();
        var assets = new AssetService(db);
        var block = (await assets.AddNodeAsync(assetId, new StructureNodeInput("Block A", StructureNodeType.Block, null, 0))).Value;
        var floor = (await assets.AddNodeAsync(assetId, new StructureNodeInput("Floor 1", StructureNodeType.Floor, block, 0))).Value;

        var result = await assets.UpdateNodeAsync(block, new StructureNodeInput("Block A", StructureNodeType.Block, floor, 0));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Property_with_units_cannot_be_deleted()
    {
        var (db, user, assetId) = await SetupAsync();
        await new UnitService(db, new FakeSubscriptions(null), user).CreateAsync(Unit(assetId, "1"));

        Assert.False((await new AssetService(db).DeleteAsync(assetId)).Succeeded);
    }

    [Fact]
    public async Task Another_business_cannot_see_or_use_the_property()
    {
        var (db, user, assetId) = await SetupAsync(businessId: 1);
        var units = new UnitService(db, new FakeSubscriptions(null), user);
        await units.CreateAsync(Unit(assetId, "1"));
        db.ChangeTracker.Clear();

        user.BusinessId = 2;
        var assets = new AssetService(db);
        Assert.Empty(await assets.ListAsync());
        Assert.Null(await assets.GetDetailsAsync(assetId));
        Assert.False((await units.CreateAsync(Unit(assetId, "2"))).Succeeded);   // "Property not found"
        Assert.Equal(0, (await units.GetUsageAsync()).Used);
    }

    [Fact]
    public async Task Stats_count_lettable_units_and_rent()
    {
        var (db, user, assetId) = await SetupAsync();
        var units = new UnitService(db, new FakeSubscriptions(null), user);
        var a = (await units.CreateAsync(Unit(assetId, "1"))).Value;
        var b = (await units.CreateAsync(Unit(assetId, "2"))).Value;
        await units.CreateAsync(Unit(assetId, "3"));
        await units.ChangeStatusAsync(a, UnitStatus.Inactive);
        await units.ChangeStatusAsync(b, UnitStatus.Reserved);

        var details = await new AssetService(db).GetDetailsAsync(assetId);
        Assert.Equal(3, details!.Stats.Total);
        Assert.Equal(2, details.Stats.Lettable);            // inactive excluded
        Assert.Equal(50_000m, details.Stats.LettableRent);
    }
}
