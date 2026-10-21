using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class CapabilityTests
{
    [Theory]
    [InlineData(AssetType.Land, PropertyPurpose.Sell)]
    [InlineData(AssetType.Estate, PropertyPurpose.Let)]
    [InlineData(AssetType.Building, PropertyPurpose.Let)]
    public void Default_purpose_by_property_type(AssetType type, PropertyPurpose expected)
        => Assert.Equal(expected, PropertyRules.DefaultPurpose(type));

    [Theory]
    [InlineData("STARTER", null, false, false)]
    [InlineData("PROFESSIONAL", null, false, false)]
    [InlineData("AGENT", null, false, true)]
    [InlineData("ENTERPRISE", null, false, true)]
    [InlineData("STARTER", null, true, true)]        // everything during the trial
    [InlineData("PROFESSIONAL", true, false, true)]  // an admin can include it on any plan
    public void Agency_by_plan(string code, bool? includes, bool trial, bool allowed)
        => Assert.Equal(allowed, PlanRules.AllowsAgency(code, includes, trial));

    private static async Task<(TestDbContext Db, FakeCurrentUser User, CapabilityService Caps, int BusinessId)> SetupAsync(BusinessType type, string planCode, bool trial)
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Cosmas Enterprises", Type = type };
        var plan = new SubscriptionPlan { Code = planCode, Name = planCode, UnitLimit = 100 };
        db.AddRange(business, plan);
        await db.SaveChangesAsync();
        db.Subscriptions.Add(new Subscription { BusinessId = business.Id, PlanId = plan.Id, IsTrial = trial,
            PeriodStartUtc = DateTime.UtcNow.AddDays(-5), PeriodEndUtc = DateTime.UtcNow.AddDays(25) });
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new SystemConfigService(db, cache);
        var subscriptions = new SubscriptionService(db, config, new FakeStorage(), cache, new FakeClock(DateTimeOffset.UtcNow));
        return (db, user, new CapabilityService(db, subscriptions, new MemoryCache(new MemoryCacheOptions()), user), business.Id);
    }

    [Fact]
    public async Task Menus_follow_the_properties()
    {
        var (db, _, caps, businessId) = await SetupAsync(BusinessType.Owner, "PROFESSIONAL", trial: false);
        var start = await caps.GetAsync();
        Assert.False(start.Rentals);   // no properties yet: no activity menus until there's a property that needs them
        Assert.False(start.Sales);

        db.Assets.Add(new Asset { Name = "Lions Shamba", Type = AssetType.Land });   // older row style: no purpose set
        await db.SaveChangesAsync();
        caps.Invalidate(businessId);
        var land = await caps.GetAsync();
        Assert.True(land.Sales);
        Assert.False(land.Rentals);    // only land for sale: no rental menus

        db.Assets.Add(new Asset { Name = "Bethlehem Estate", Type = AssetType.Estate, Purpose = PropertyPurpose.Let });
        await db.SaveChangesAsync();
        caps.Invalidate(businessId);
        var both = await caps.GetAsync();
        Assert.True(both.Rentals && both.Sales);
        Assert.False(both.Agency);
    }

    [Fact]
    public async Task Agency_needs_the_setting_and_a_plan_that_includes_it()
    {
        var (_, _, onTrial, _) = await SetupAsync(BusinessType.Agency, "PROFESSIONAL", trial: true);
        Assert.True((await onTrial.GetAsync()).Agency);

        var (_, _, onProfessional, _) = await SetupAsync(BusinessType.Agency, "PROFESSIONAL", trial: false);
        var pro = await onProfessional.GetAsync();
        Assert.True(pro.AgencyOn);
        Assert.False(pro.Agency);       // switched on, but the plan doesn't include it

        var (_, _, onAgent, _) = await SetupAsync(BusinessType.Agency, "AGENT", trial: false);
        Assert.True((await onAgent.GetAsync()).Agency);
    }
}
