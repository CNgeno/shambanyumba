using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class BillingRulesTests
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1), Oct31 = new(2026, 10, 31);

    [Fact]
    public void Full_month_when_lease_started_earlier()
        => Assert.Equal(30_000m, BillingRules.RentForPeriod(30_000m, new DateOnly(2026, 3, 1), Oct1, Oct31).Amount);

    [Fact]
    public void First_month_is_prorated()
    {
        var (amount, days, inPeriod) = BillingRules.RentForPeriod(31_000m, new DateOnly(2026, 10, 20), Oct1, Oct31);
        Assert.Equal((12, 31), (days, inPeriod));
        Assert.Equal(12_000m, amount);
    }

    [Fact]
    public void Nothing_before_the_lease_starts()
        => Assert.Equal(0m, BillingRules.RentForPeriod(30_000m, new DateOnly(2026, 11, 3), Oct1, Oct31).Amount);

    [Theory]
    [InlineData(2026, 2, 28, 1, 28)]   // due day 28 in February
    [InlineData(2026, 10, 5, 1, 5)]
    public void Due_date_uses_the_rent_due_day(int year, int month, int dueDay, int leaseStartDay, int expectedDay)
        => Assert.Equal(new DateOnly(year, month, expectedDay),
            BillingRules.DueDate(new DateOnly(year, month, 1), dueDay, new DateOnly(2025, 1, leaseStartDay)));

    [Fact]
    public void Due_date_is_never_before_the_lease_starts()
        => Assert.Equal(new DateOnly(2026, 10, 20), BillingRules.DueDate(Oct1, 5, new DateOnly(2026, 10, 20)));

    [Theory]
    [InlineData(false, 20, false, true)]   // new lease starting this month
    [InlineData(true, 20, false, false)]   // renewal: deposit carried over
    [InlineData(false, -60, false, false)] // onboarded tenant who started months ago
    [InlineData(false, 20, true, false)]   // already invoiced
    public void Deposit_only_for_brand_new_leases(bool renewal, int startOffsetDays, bool alreadyInvoiced, bool expected)
        => Assert.Equal(expected, BillingRules.ShouldInvoiceDeposit(30_000m, renewal, Oct1.AddDays(startOffsetDays), Oct1, alreadyInvoiced));

    [Fact]
    public void Line_math_rounds_tax_per_line()
    {
        var (amount, tax, total) = BillingRules.LineMath(1, 2_000m, 16m);
        Assert.Equal((2_000m, 320m, 2_320m), (amount, tax, total));
        Assert.Equal(1_837.50m, BillingRules.LineMath(12.25m, 150m, 0).Amount);
    }

    [Theory]
    [InlineData(InvoiceStatus.Draft, 0, 10, InvoiceStatus.Draft)]
    [InlineData(InvoiceStatus.Pending, 0, 10, InvoiceStatus.Pending)]
    [InlineData(InvoiceStatus.Pending, 0, -1, InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Pending, 500, 10, InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Pending, 500, -1, InvoiceStatus.Overdue)]
    [InlineData(InvoiceStatus.Pending, 1000, -1, InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Cancelled, 0, -1, InvoiceStatus.Cancelled)]
    public void Effective_status(InvoiceStatus stored, int paid, int daysUntilDue, InvoiceStatus expected)
        => Assert.Equal(expected, BillingRules.EffectiveStatus(stored, 1000m, paid, Oct1.AddDays(daysUntilDue), Oct1));
}

public class BillingServiceTests
{
    // 11 Oct 2026, 10:00 EAT
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int LeaseId, int UnitId, int WaterId)
    {
        public BillingRunService Runs() => new(Db, new NumberSequenceService(Db),
            new CreditService(Db, User, new SystemConfigService(Db, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())), Clock), Clock);
        public InvoiceService Invoices() => new(Db, new NumberSequenceService(Db), Clock);
        public ChargeService Charges() => new(Db, Clock);
        public BillingRunRequest October(bool issue = false) => new(2026, 10, null, true, issue);
    }

    /// <summary>One active lease (started in March, KES 30,000) with garbage KES 300 and metered water at KES 150/m³.</summary>
    private static async Task<Ctx> SetupAsync(int businessId = 1)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = businessId;
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Estate };
        var unit = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 30_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Mercy Achieng" };
        var lease = new Lease
        {
            LeaseNumber = "L-00001", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active,
            StartDate = new DateOnly(2026, 3, 1), MonthlyRent = 30_000m, DepositAmount = 30_000m, RentDueDay = 5
        };
        var garbage = new ChargeDefinition { Name = "Garbage", Type = ChargeType.Recurring, DefaultAmount = 300m };
        var water = new ChargeDefinition { Name = "Water", Type = ChargeType.Utility, UnitRate = 150m, UnitOfMeasure = "m³" };
        db.AddRange(asset, unit, tenant, lease, garbage, water,
            new LeaseCharge { Lease = lease, ChargeDefinition = garbage },
            new LeaseCharge { Lease = lease, ChargeDefinition = water });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), lease.Id, unit.Id, water.Id);
    }

    private static async Task ReadWaterAsync(Ctx c, decimal opening, decimal current)
    {
        Assert.True((await c.Charges().SaveReadingsAsync(c.WaterId, new DateOnly(2026, 9, 30), [new MeterReadingEntry(c.UnitId, opening, true)])).Succeeded);
        var r = await c.Charges().SaveReadingsAsync(c.WaterId, new DateOnly(2026, 10, 10), [new MeterReadingEntry(c.UnitId, current, false)]);
        Assert.True(r.Succeeded, r.Error);
        c.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Monthly_run_bills_rent_charges_and_water_once()
    {
        var c = await SetupAsync();
        await ReadWaterAsync(c, 100m, 112.5m);   // 12.5 m³ x 150 = 1,875

        var first = await c.Runs().GenerateAsync(c.October());
        Assert.Equal(1, first.Created);
        Assert.Equal(30_000m + 300m + 1_875m, first.TotalInvoiced);

        var invoice = await c.Db.Invoices.Include(i => i.Lines).SingleAsync();
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(new DateOnly(2026, 10, 5), invoice.DueDate);
        Assert.DoesNotContain(invoice.Lines, l => l.LineType == InvoiceLineType.Deposit);   // existing tenant
        Assert.NotNull((await c.Db.UtilityMeterReadings.SingleAsync(r => !r.IsOpening)).BilledInvoiceId);

        var second = await c.Runs().GenerateAsync(c.October());
        Assert.Equal((0, 1), (second.Created, second.Skipped));
        Assert.Equal(1, await c.Db.Invoices.CountAsync());
    }

    [Fact]
    public async Task New_lease_mid_month_is_prorated_and_gets_its_deposit()
    {
        var c = await SetupAsync();
        var lease = await c.Db.Leases.SingleAsync();
        lease.StartDate = new DateOnly(2026, 10, 20);
        lease.MonthlyRent = 31_000m;
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await c.Runs().GenerateAsync(c.October());
        var lines = await c.Db.InvoiceLines.ToListAsync();
        Assert.Equal(12_000m, lines.Single(l => l.LineType == InvoiceLineType.Rent).Amount);
        Assert.Equal(30_000m, lines.Single(l => l.LineType == InvoiceLineType.Deposit).Amount);
        Assert.Equal(new DateOnly(2026, 10, 20), (await c.Db.Invoices.SingleAsync()).DueDate);
    }

    [Fact]
    public async Task Cancelling_an_invoice_frees_its_readings_for_the_next_run()
    {
        var c = await SetupAsync();
        await ReadWaterAsync(c, 100m, 110m);
        await c.Runs().GenerateAsync(c.October(issue: true));
        var id = (await c.Db.Invoices.SingleAsync()).Id;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Invoices().CancelAsync(id, "")).Succeeded);   // reason required
        Assert.True((await c.Invoices().CancelAsync(id, "Wrong rent")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Null((await c.Db.UtilityMeterReadings.SingleAsync(r => !r.IsOpening)).BilledInvoiceId);

        var rerun = await c.Runs().GenerateAsync(c.October());
        Assert.Equal(1, rerun.Created);   // the cancelled invoice doesn't block the month
    }

    [Fact]
    public async Task Readings_must_go_up_and_the_first_is_an_opening()
    {
        var c = await SetupAsync();
        Assert.True((await c.Charges().SaveReadingsAsync(c.WaterId, new DateOnly(2026, 9, 30), [new MeterReadingEntry(c.UnitId, 100m, false)])).Succeeded);
        Assert.True((await c.Db.UtilityMeterReadings.SingleAsync()).IsOpening);   // no previous reading -> opening
        c.Db.ChangeTracker.Clear();

        var lower = await c.Charges().SaveReadingsAsync(c.WaterId, new DateOnly(2026, 10, 10), [new MeterReadingEntry(c.UnitId, 90m, false)]);
        Assert.False(lower.Succeeded);
        var future = await c.Charges().SaveReadingsAsync(c.WaterId, new DateOnly(2026, 12, 1), [new MeterReadingEntry(c.UnitId, 120m, false)]);
        Assert.False(future.Succeeded);
    }

    [Fact]
    public async Task Lease_rate_override_is_used_for_readings()
    {
        var c = await SetupAsync();
        var charge = await c.Db.LeaseCharges.SingleAsync(l => l.ChargeDefinitionId == c.WaterId);
        charge.UnitRateOverride = 120m;
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await ReadWaterAsync(c, 100m, 110m);
        Assert.Equal(1_200m, (await c.Db.UtilityMeterReadings.SingleAsync(r => !r.IsOpening)).Amount);
    }

    [Fact]
    public async Task Manual_invoice_totals_include_vat_and_only_drafts_change()
    {
        var c = await SetupAsync();
        var tenantId = (await c.Db.Tenants.SingleAsync()).Id;
        var input = new ManualInvoiceInput(tenantId, c.LeaseId, Oct1, Oct1.AddDays(7), null,
            [new InvoiceLineInput(InvoiceLineType.ServiceCharge, "Service charge", 1, 2_000m, 16m),
             new InvoiceLineInput(InvoiceLineType.Other, "Key replacement", 2, 750m, 0m)]);

        var id = (await c.Invoices().CreateManualAsync(input, issue: true)).Value;
        var invoice = await c.Db.Invoices.SingleAsync(i => i.Id == id);
        Assert.Equal((3_500m, 320m, 3_820m), (invoice.SubTotal, invoice.TaxTotal, invoice.Total));
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Invoices().UpdateDraftAsync(id, input)).Succeeded);   // issued
    }

    [Fact]
    public async Task Another_business_cannot_see_or_cancel_invoices()
    {
        var c = await SetupAsync(businessId: 1);
        await c.Runs().GenerateAsync(c.October(issue: true));
        var id = (await c.Db.Invoices.SingleAsync()).Id;
        c.Db.ChangeTracker.Clear();

        c.User.BusinessId = 2;
        Assert.Null(await c.Invoices().GetDetailsAsync(id));
        Assert.False((await c.Invoices().CancelAsync(id, "test")).Succeeded);
        Assert.Equal(0, (await c.Runs().GenerateAsync(c.October())).Created);
    }
}
