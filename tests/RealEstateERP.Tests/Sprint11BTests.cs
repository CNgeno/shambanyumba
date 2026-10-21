using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class ChargeFrequencyTests
{
    [Theory]
    [InlineData(ChargeFrequency.Monthly, 1, 6, true)]
    [InlineData(ChargeFrequency.Quarterly, 1, 1, true)]
    [InlineData(ChargeFrequency.Quarterly, 1, 4, true)]
    [InlineData(ChargeFrequency.Quarterly, 1, 10, true)]
    [InlineData(ChargeFrequency.Quarterly, 1, 11, false)]
    [InlineData(ChargeFrequency.Quarterly, 11, 2, true)]    // Nov, Feb, May, Aug
    [InlineData(ChargeFrequency.Annual, 7, 7, true)]
    [InlineData(ChargeFrequency.Annual, 7, 8, false)]
    public void Charge_due_months(ChargeFrequency frequency, int anchor, int month, bool due)
        => Assert.Equal(due, BillingRules.ChargeDue(frequency, anchor, month));

    [Fact]
    public async Task Billing_run_includes_a_quarterly_charge_only_in_its_months()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Building };
        var unit = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 20_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Faith" };
        var lease = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 20_000m, RentDueDay = 5 };
        var insurance = new ChargeDefinition { Name = "Insurance", Type = ChargeType.Recurring, DefaultAmount = 3_000m, Frequency = ChargeFrequency.Quarterly, AnchorMonth = 1 };
        db.AddRange(asset, unit, tenant, lease, insurance, new LeaseCharge { Lease = lease, ChargeDefinition = insurance });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var clock = new FakeClock(new DateTimeOffset(2026, 10, 11, 7, 0, 0, TimeSpan.Zero));
        var config = new SystemConfigService(db, new MemoryCache(new MemoryCacheOptions()));
        var runs = new BillingRunService(db, new NumberSequenceService(db), new CreditService(db, user, config, clock), clock);

        var october = Assert.Single(await runs.PreviewAsync(new BillingRunRequest(2026, 10, null, false, false)));
        Assert.Equal(23_000m, october.EstimatedTotal);   // rent + the quarter's insurance
        var november = Assert.Single(await runs.PreviewAsync(new BillingRunRequest(2026, 11, null, false, false)));
        Assert.Equal(20_000m, november.EstimatedTotal);  // rent only
    }
}

public class NotificationTests
{
    [Fact]
    public async Task Unread_count_resets_when_read()
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Acacia" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;
        user.UserId = "owner";
        db.BusinessUsers.Add(new BusinessUser { BusinessId = business.Id, UserId = "owner", Role = BusinessRole.Owner });
        NotificationLog.Add(db, business.Id, NotificationType.Lead, "New lead: Faith", "About A1", "/Leads");
        NotificationLog.Add(db, business.Id, NotificationType.Automation, "Automatic run", "3 invoices created.");
        await db.SaveChangesAsync();

        var clock = new FakeClock(DateTimeOffset.UtcNow.AddMinutes(1));
        var service = new NotificationService(db, user, clock);
        Assert.Equal(2, await service.UnreadCountAsync());
        Assert.All(await service.RecentAsync(10), n => Assert.True(n.IsUnread));

        await service.MarkAllReadAsync();
        Assert.Equal(0, await service.UnreadCountAsync());
        user.UserId = "someone-else";   // not a member: sees nothing
        Assert.Empty(await service.RecentAsync(10));
    }
}
