using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class ArrearsRulesTests
{
    [Theory]
    [InlineData(1, AgingBucket.Days1To30)]
    [InlineData(30, AgingBucket.Days1To30)]
    [InlineData(31, AgingBucket.Days31To60)]
    [InlineData(90, AgingBucket.Days61To90)]
    [InlineData(91, AgingBucket.Over90)]
    public void Buckets(int days, AgingBucket expected) => Assert.Equal(expected, ArrearsRules.Bucket(days));

    [Fact]
    public void Penalty_waits_for_the_whole_grace_period()
    {
        var due = new DateOnly(2026, 10, 5);
        Assert.False(ArrearsRules.PenaltyDue(due, 5, new DateOnly(2026, 10, 10)));   // last day of grace
        Assert.True(ArrearsRules.PenaltyDue(due, 5, new DateOnly(2026, 10, 11)));
    }

    [Fact]
    public void Fixed_penalty_wins_over_percentage()
    {
        Assert.Equal(1_000m, ArrearsRules.PenaltyAmount(30_000m, 1_000m, 5m));
        Assert.Equal(1_500m, ArrearsRules.PenaltyAmount(30_000m, null, 5m));
        Assert.Equal(7, ArrearsRules.GraceDays(7, 5));
        Assert.Equal(5, ArrearsRules.GraceDays(null, 5));
    }
}

public class ArrearsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int TenantId, int UnitId, int LeaseId)
    {
        public ArrearsService Arrears()
            => new(Db, new NumberSequenceService(Db), new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), Clock);

        public async Task<int> InvoiceAsync(string number, int daysOverdue, decimal total, decimal paid = 0)
        {
            var inv = new Invoice
            {
                InvoiceNumber = number, Kind = InvoiceKind.Monthly, TenantId = TenantId, LeaseId = LeaseId, Status = InvoiceStatus.Pending,
                InvoiceDate = Today.AddDays(-daysOverdue - 4), DueDate = Today.AddDays(-daysOverdue), SubTotal = total, Total = total, AmountPaid = paid
            };
            Db.Invoices.Add(inv);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
            return inv.Id;
        }
    }

    private static async Task<Ctx> SetupAsync(decimal? unitFixedPenalty = null)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        db.SystemConfigurations.AddRange(
            new SystemConfiguration { Key = ConfigKeys.DefaultGraceDaysAfterDue, Value = "5", ValueType = ConfigValueType.Integer },
            new SystemConfiguration { Key = ConfigKeys.DefaultPenaltyPercent, Value = "5", ValueType = ConfigValueType.Decimal });
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Estate };
        var unit = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 10_000m, Status = UnitStatus.Occupied, LatePenaltyAmount = unitFixedPenalty };
        var tenant = new Tenant { FullName = "David Ochieng" };
        var lease = new Lease { LeaseNumber = "L-00001", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 10_000m };
        db.AddRange(asset, unit, tenant, lease);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), tenant.Id, unit.Id, lease.Id);
    }

    [Fact]
    public async Task Report_ages_overdue_balances()
    {
        var c = await SetupAsync();
        await c.InvoiceAsync("INV-1", 10, 10_000m, paid: 4_000m);   // 6,000 in 1-30
        await c.InvoiceAsync("INV-2", 45, 10_000m);                 // 31-60
        await c.InvoiceAsync("INV-3", 100, 10_000m);                // 90+
        await c.InvoiceAsync("INV-4", -5, 10_000m);                 // not yet due

        var report = await c.Arrears().GetReportAsync(null);
        Assert.Equal((6_000m, 10_000m, 0m, 10_000m), (report.Totals.Days1To30, report.Totals.Days31To60, report.Totals.Days61To90, report.Totals.Over90));
        Assert.Equal(10_000m, report.NotYetDue);
        var tenant = Assert.Single(report.Tenants);
        Assert.Equal(100, tenant.OldestDaysOverdue);
    }

    [Fact]
    public async Task Penalty_after_grace_is_charged_once()
    {
        var c = await SetupAsync();
        var late = await c.InvoiceAsync("INV-1", 10, 10_000m);
        await c.InvoiceAsync("INV-2", 3, 10_000m);   // still within the 5-day grace period

        var preview = await c.Arrears().PreviewPenaltiesAsync();
        var candidate = Assert.Single(preview);
        Assert.Equal((late, 500m), (candidate.InvoiceId, candidate.Penalty));   // 5% of 10,000

        var run = await c.Arrears().ApplyPenaltiesAsync(null);
        Assert.Equal(1, run.Created);
        var penalty = await c.Db.Invoices.SingleAsync(i => i.Kind == InvoiceKind.Penalty);
        Assert.Equal((late, 500m, InvoiceStatus.Pending), (penalty.PenaltyForInvoiceId!.Value, penalty.Total, penalty.Status));

        Assert.Empty(await c.Arrears().PreviewPenaltiesAsync());
        Assert.Equal(0, (await c.Arrears().ApplyPenaltiesAsync(null)).Created);
    }

    [Fact]
    public async Task Unit_fixed_penalty_is_used_and_penalties_are_never_penalised()
    {
        var c = await SetupAsync(unitFixedPenalty: 1_000m);
        await c.InvoiceAsync("INV-1", 10, 10_000m);
        await c.Arrears().ApplyPenaltiesAsync(null);
        Assert.Equal(1_000m, (await c.Db.Invoices.SingleAsync(i => i.Kind == InvoiceKind.Penalty)).Total);

        c.Clock.Advance(TimeSpan.FromDays(40));   // the penalty invoice is now long overdue too
        Assert.Empty(await c.Arrears().PreviewPenaltiesAsync());
    }

    [Fact]
    public async Task Collections_note_moves_the_stage_and_checks_promises()
    {
        var c = await SetupAsync();
        Assert.True((await c.Arrears().AddNoteAsync(new CollectionNoteInput(c.TenantId, CollectionStage.DemandServed, "Demand letter delivered", null), null)).Succeeded);
        c.Db.ChangeTracker.Clear();

        Assert.Equal(CollectionStage.DemandServed, (await c.Db.Tenants.SingleAsync()).CollectionStage);
        Assert.True((await c.Arrears().GetNotesAsync(c.TenantId)).Single().StageChanged);
        Assert.False((await c.Arrears().AddNoteAsync(new CollectionNoteInput(c.TenantId, CollectionStage.DemandServed, "Promise", Today.AddDays(-1)), null)).Succeeded);
    }
}

public class CreditTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

    private static (TestDbContext Db, FakeCurrentUser User, FakeClock Clock, MemoryCache Cache) Create()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = null;
        return (db, user, new FakeClock(Now), new MemoryCache(new MemoryCacheOptions()));
    }

    [Fact]
    public async Task New_business_gets_trial_credits_with_a_ledger_entry()
    {
        var (db, _, clock, cache) = Create();
        db.SystemConfigurations.Add(new SystemConfiguration { Key = ConfigKeys.TrialPlanCode, Value = "PRO" });
        db.SubscriptionPlans.Add(new SubscriptionPlan { Code = "PRO", Name = "Professional", CreditsPerMonth = 200, TrialDays = 14, MonthlyFee = 4_500m });
        await db.SaveChangesAsync();

        var subscriptions = new SubscriptionService(db, new SystemConfigService(db, cache), new FakeStorage(), cache, clock);
        var id = await new BusinessService(db, subscriptions, new FakeStorage(), new SystemConfigService(db, cache)).CreateBusinessAsync("owner-1",
            new CreateBusinessRequest("Test Estates", BusinessType.Owner, null, null, null, null));
        db.ChangeTracker.Clear();

        Assert.Equal(200, (await db.Businesses.SingleAsync(b => b.Id == id)).CreditBalance);
        var entry = await db.CreditTransactions.SingleAsync(t => t.BusinessId == id);
        Assert.Equal((CreditTransactionType.Allocation, 200, 200), (entry.Type, entry.Credits, entry.BalanceAfter));
    }

    [Fact]
    public async Task Consuming_credits_is_refused_when_the_balance_is_too_low()
    {
        var (db, user, clock, cache) = Create();
        var business = new Business { Name = "Test Estates" };
        db.Businesses.Add(business);
        db.CreditTransactions.Add(CreditLedger.Post(business, 2, CreditTransactionType.Allocation, "Start"));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        user.BusinessId = business.Id;

        var credits = new CreditService(db, user, new SystemConfigService(db, cache), clock);
        Assert.True((await credits.TryConsumeAsync(CreditCosts.InvoicePdf, "Invoice PDF INV-1", "INV-1")).Succeeded);
        db.ChangeTracker.Clear();
        Assert.False((await credits.TryConsumeAsync(CreditCosts.StatementPdf, "Statement PDF", null)).Succeeded);
        db.ChangeTracker.Clear();

        Assert.Equal(1, await credits.GetBalanceAsync());
        var last = await db.CreditTransactions.OrderByDescending(t => t.Id).FirstAsync();
        Assert.Equal((-1, 1), (last.Credits, last.BalanceAfter));   // ledger matches the balance
    }

    [Fact]
    public async Task Approved_purchase_adds_credits_once()
    {
        var (db, user, clock, cache) = Create();
        db.SystemConfigurations.AddRange(
            new SystemConfiguration { Key = ConfigKeys.CreditBundles, Value = "100,500" },
            new SystemConfiguration { Key = ConfigKeys.CreditPrice, Value = "5", ValueType = ConfigValueType.Decimal });
        var business = new Business { Name = "Test Estates" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;

        var credits = new CreditService(db, user, new SystemConfigService(db, cache), clock);
        Assert.False((await credits.RequestPurchaseAsync(new CreditPurchaseInput(123, SubscriptionPaymentMethod.Mpesa, "SJK4H7QX2P"))).Succeeded);
        var id = (await credits.RequestPurchaseAsync(new CreditPurchaseInput(100, SubscriptionPaymentMethod.Mpesa, "SJK4H7QX2P"))).Value;
        Assert.Equal(500m, (await db.CreditPurchases.SingleAsync()).Amount);
        db.ChangeTracker.Clear();

        Assert.True((await credits.ApprovePurchaseAsync(id, "admin")).Succeeded);
        db.ChangeTracker.Clear();
        Assert.False((await credits.ApprovePurchaseAsync(id, "admin")).Succeeded);
        Assert.Equal(100, await credits.GetBalanceAsync());
    }
}
