using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class RunningCostTests
{
    [Theory]
    [InlineData(CostCategory.Construction, false)]
    [InlineData(CostCategory.Letting, true)]
    [InlineData(CostCategory.Repairs, true)]
    [InlineData(CostCategory.Cleaning, true)]
    [InlineData(CostCategory.Advertising, true)]
    public void Running_costs_are_separate_from_development_costs(CostCategory category, bool running)
    {
        Assert.Equal(running, CostingRules.IsRunningCost(category));
        Assert.Equal(!running, CostingRules.IsDevelopmentCost(category));
    }

    [Fact]
    public async Task Net_income_is_rent_collected_less_running_costs()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 15, 7, 0, 0, TimeSpan.Zero));
        var asset = new Asset { Name = "Bethlehem Estate", Type = AssetType.Estate };
        var unit = new Unit { Asset = asset, UnitNumber = "H1", MonthlyRent = 6_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Mary" };
        var lease = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 6_000m, RentDueDay = 5 };
        var invoice = new Invoice { InvoiceNumber = "INV-1", Tenant = tenant, Lease = lease, Status = InvoiceStatus.Paid, InvoiceDate = new DateOnly(2026, 10, 1),
            DueDate = new DateOnly(2026, 10, 5), Total = 6_000m, AmountPaid = 6_000m };
        var payment = new Payment { ReceiptNumber = "RCT-1", Tenant = tenant, PaymentDate = new DateOnly(2026, 10, 3), Amount = 6_000m, Method = PaymentMethod.Mpesa };
        payment.Allocations.Add(new PaymentAllocation { Invoice = invoice, Amount = 6_000m });
        db.AddRange(asset, unit, tenant, lease, invoice, payment,
            new ProjectCost { Asset = asset, Kind = CostKind.Actual, Category = CostCategory.Repairs, Description = "Plumbing H1", Amount = 1_500m, Date = new DateOnly(2026, 10, 10) },
            new ProjectCost { Asset = asset, Kind = CostKind.Actual, Category = CostCategory.Cleaning, Description = "Compound cleaning", Amount = 500m, Date = new DateOnly(2026, 6, 1) });
        await db.SaveChangesAsync();

        var money = (await new ReportService(db, clock).GetPropertyMoneyAsync())[asset.Id];
        Assert.Equal((6_000m, 1_500m, 4_500m), (money.CollectedThisMonth, money.ExpensesThisMonth, money.NetThisMonth));
        Assert.Equal((6_000m, 2_000m, 4_000m), (money.Collected12Months, money.Expenses12Months, money.Net12Months));
    }
}

public class FinancialAccessTests
{
    [Theory]
    [InlineData(BusinessRole.Owner, false, true)]
    [InlineData(BusinessRole.Admin, false, true)]
    [InlineData(BusinessRole.User, false, false)]   // a caretaker: day-to-day work, no finances
    [InlineData(BusinessRole.User, true, true)]     // allowed by an Owner or Admin
    [InlineData(BusinessRole.Viewer, false, false)]
    public async Task Finances_are_private_unless_allowed(BusinessRole role, bool allowed, bool sees)
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Cosmas Enterprises" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        db.BusinessUsers.Add(new BusinessUser { BusinessId = business.Id, UserId = "member", Role = role, CanViewFinancials = allowed });
        await db.SaveChangesAsync();
        (user.BusinessId, user.UserId, user.Role) = (business.Id, "member", role);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new SystemConfigService(db, cache);
        var caps = new CapabilityService(db, new SubscriptionService(db, config, new FakeStorage(), cache, new FakeClock(DateTimeOffset.UtcNow)), cache, user);
        Assert.Equal(sees, await caps.CanViewFinancialsAsync());
    }
}
