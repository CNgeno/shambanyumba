using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class SalesRulesTests
{
    private static readonly DateOnly SaleDay = new(2026, 1, 15);

    [Fact]
    public void Schedule_adds_up_to_the_price_with_rounding_on_the_last_line()
    {
        var lines = SalesRules.BuildSchedule(1_450_000m, 290_000m, PaymentPlan.Instalments, 12, InstalmentFrequency.Monthly, SaleDay, new DateOnly(2026, 2, 15));
        Assert.Equal(13, lines.Count);
        Assert.Equal(1_450_000m, lines.Sum(l => l.Amount));
        Assert.Equal(("Deposit", SaleDay, 290_000m), (lines[0].Description, lines[0].DueDate, lines[0].Amount));
        Assert.Equal(96_666.66m, lines[1].Amount);
        Assert.Equal(96_666.74m, lines[12].Amount);
        Assert.Equal(new DateOnly(2027, 1, 15), lines[12].DueDate);
    }

    [Fact]
    public void Quarterly_and_full_payment()
    {
        var q = SalesRules.BuildSchedule(1_000_000m, 0, PaymentPlan.Instalments, 4, InstalmentFrequency.Quarterly, SaleDay, new DateOnly(2026, 4, 1));
        Assert.Equal(new[] { new DateOnly(2026, 4, 1), new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 1), new DateOnly(2027, 1, 1) }, q.Select(l => l.DueDate));
        Assert.DoesNotContain(q, l => l.Number == 0);   // no deposit line when the deposit is zero

        var full = Assert.Single(SalesRules.BuildSchedule(2_000_000m, 0, PaymentPlan.FullPayment, 0, InstalmentFrequency.Monthly, SaleDay, null));
        Assert.Equal((2_000_000m, SaleDay), (full.Amount, full.DueDate));
    }

    [Fact]
    public void Plan_validation()
    {
        Assert.Null(SalesRules.ValidatePlan(1_000m, 200m, PaymentPlan.Instalments, 6, SaleDay, SaleDay.AddMonths(1)));
        Assert.NotNull(SalesRules.ValidatePlan(1_000m, 1_000m, PaymentPlan.Instalments, 6, SaleDay, SaleDay.AddMonths(1)));   // deposit = price
        Assert.NotNull(SalesRules.ValidatePlan(1_000m, 200m, PaymentPlan.Instalments, 0, SaleDay, SaleDay.AddMonths(1)));
        Assert.NotNull(SalesRules.ValidatePlan(1_000m, 200m, PaymentPlan.Instalments, 6, SaleDay, SaleDay));                // first on the sale day
    }

    [Fact]
    public void Milestones_go_in_order_and_handover_needs_full_payment()
    {
        Assert.Null(SalesRules.MilestoneError(MilestoneType.Agreement, [], 1_000m));
        Assert.NotNull(SalesRules.MilestoneError(MilestoneType.TitleTransfer, [MilestoneType.Agreement], 0));
        Assert.NotNull(SalesRules.MilestoneError(MilestoneType.Handover, [MilestoneType.Agreement, MilestoneType.StampDuty, MilestoneType.TitleTransfer], 1m));
        Assert.Null(SalesRules.MilestoneError(MilestoneType.Handover, [MilestoneType.Agreement, MilestoneType.StampDuty, MilestoneType.TitleTransfer], 0));
    }

    [Fact]
    public void Instalment_states()
    {
        var today = new DateOnly(2026, 6, 1);
        Assert.Equal(InstalmentState.Paid, SalesRules.State(100, 100, today.AddDays(-10), today));
        Assert.Equal(InstalmentState.Overdue, SalesRules.State(100, 40, today.AddDays(-1), today));
        Assert.Equal(InstalmentState.PartlyPaid, SalesRules.State(100, 40, today.AddDays(5), today));
        Assert.Equal(InstalmentState.Due, SalesRules.State(100, 0, today, today));
    }
}

public class SaleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, MemoryCache Cache, int BuyerId, int PlotA, int PlotB, int AgentId)
    {
        public AgentScopeService Scope() => new(Db, User, Cache);
        public SaleService Sales() => new(Db, new NumberSequenceService(Db), Scope(), new FakeStorage(),
            new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), User, Clock);
        public UnitService Units() => new(Db, new FakeSubscriptions(unitLimit: 100), User);

        public SaleInput Plan(int unitId, int? agentId = null, decimal commission = 0)
            => new(BuyerId, unitId, agentId, Today.AddMonths(-3), 1_200_000m, PaymentPlan.Instalments, 300_000m, 3,
                InstalmentFrequency.Monthly, Today.AddMonths(-2), commission, null);

        public async Task<UnitStatus> UnitStatusAsync(int id) => (await Db.Units.AsNoTracking().SingleAsync(u => u.Id == id)).Status;
    }

    private static async Task<Ctx> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        user.BusinessType = BusinessType.Developer;
        user.UserId = "owner";
        var asset = new Asset { Name = "Savanna Heights", Type = AssetType.Land };
        var a = new Unit { Asset = asset, UnitNumber = "Plot 1", Type = UnitType.Plot, SalePrice = 1_200_000m, Status = UnitStatus.Available };
        var b = new Unit { Asset = asset, UnitNumber = "Plot 2", Type = UnitType.Plot, SalePrice = 1_200_000m, Status = UnitStatus.Available };
        var buyer = new Buyer { FullName = "Ruth Achieng" };
        var agent = new Agent { FullName = "James Kamau", CommissionSharePercent = 40m, UserId = "james" };
        db.AddRange(asset, a, b, buyer, agent);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), new MemoryCache(new MemoryCacheOptions()), buyer.Id, a.Id, b.Id, agent.Id);
    }

    [Fact]
    public async Task Creating_a_sale_reserves_the_unit_and_builds_the_schedule()
    {
        var c = await SetupAsync();
        var created = await c.Sales().CreateAsync(c.Plan(c.PlotA));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(UnitStatus.Reserved, await c.UnitStatusAsync(c.PlotA));

        var d = (await c.Sales().GetAsync(created.Value))!;
        Assert.Equal(4, d.Schedule.Count);   // deposit + 3
        Assert.Equal(1_200_000m, d.Schedule.Sum(i => i.Amount));
        Assert.Equal(4, d.Milestones.Count);
        Assert.Equal(SaleStatus.Reserved, d.Sale.Status);

        Assert.False((await c.Sales().CreateAsync(c.Plan(c.PlotA))).Succeeded);   // one live sale per unit
        Assert.False((await c.Units().ChangeStatusAsync(c.PlotA, UnitStatus.Available)).Succeeded);   // status follows the sale
    }

    [Fact]
    public async Task Payments_go_to_the_oldest_instalment_and_cannot_overpay()
    {
        var c = await SetupAsync();
        var id = (await c.Sales().CreateAsync(c.Plan(c.PlotA))).Value;
        c.Db.ChangeTracker.Clear();

        var paid = await c.Sales().RecordPaymentAsync(id, new SalePaymentInput(Today, 450_000m, PaymentMethod.Mpesa, "SJK4H7QX2P", null));
        Assert.True(paid.Succeeded, paid.Error);
        c.Db.ChangeTracker.Clear();

        var d = (await c.Sales().GetAsync(id))!;
        Assert.Equal(450_000m, d.Sale.AmountPaid);
        Assert.Equal(InstalmentState.Paid, d.Schedule[0].State);              // deposit 300,000
        Assert.Equal(150_000m, d.Schedule[1].AmountPaid);                     // then 150,000 of instalment 1
        Assert.Equal(InstalmentState.Overdue, d.Schedule[1].State);           // due two months ago, part paid

        Assert.False((await c.Sales().RecordPaymentAsync(id, new SalePaymentInput(Today, 800_000m, PaymentMethod.Cash, null, null))).Succeeded);   // more than the balance
        Assert.False((await c.Sales().RecordPaymentAsync(id, new SalePaymentInput(Today, 1_000m, PaymentMethod.Mpesa, "SJK4H7QX2P", null))).Succeeded);   // duplicate code
    }

    [Fact]
    public async Task Agreement_sells_the_unit_and_completion_needs_payment_and_handover()
    {
        var c = await SetupAsync();
        var s = c.Sales();
        var id = (await s.CreateAsync(c.Plan(c.PlotA))).Value;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.StampDuty, Today, null, null, null)).Succeeded);   // agreement first
        Assert.True((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.Agreement, Today, "AGR-1", null, null)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(UnitStatus.Sold, await c.UnitStatusAsync(c.PlotA));

        Assert.True((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.StampDuty, Today, null, null, null)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.TitleTransfer, Today, "T/1", null, null)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.Handover, Today, null, null, null)).Succeeded);   // not paid

        var payment = (await c.Sales().RecordPaymentAsync(id, new SalePaymentInput(Today, 1_200_000m, PaymentMethod.BankTransfer, "FT-998877", null))).Value;
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Sales().CompleteMilestoneAsync(id, MilestoneType.Handover, Today, null, null, null)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(SaleStatus.Completed, (await c.Sales().GetAsync(id))!.Sale.Status);

        Assert.True((await c.Sales().ReversePaymentAsync(payment, "Transfer recalled")).Succeeded);   // reopens it
        c.Db.ChangeTracker.Clear();
        var after = (await c.Sales().GetAsync(id))!;
        Assert.Equal((SaleStatus.Active, 0m), (after.Sale.Status, after.Sale.AmountPaid));
    }

    [Fact]
    public async Task Cancelling_frees_the_unit_but_not_while_money_is_held()
    {
        var c = await SetupAsync();
        var id = (await c.Sales().CreateAsync(c.Plan(c.PlotA))).Value;
        c.Db.ChangeTracker.Clear();
        var pay = (await c.Sales().RecordPaymentAsync(id, new SalePaymentInput(Today, 50_000m, PaymentMethod.Cash, null, null))).Value;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Sales().CancelAsync(id, "Buyer withdrew", false)).Succeeded);
        Assert.True((await c.Sales().ReversePaymentAsync(pay, "Refunded")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Sales().CancelAsync(id, "Buyer withdrew", false)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(UnitStatus.Available, await c.UnitStatusAsync(c.PlotA));
        Assert.True((await c.Sales().CreateAsync(c.Plan(c.PlotA))).Succeeded);   // can be sold again
    }

    [Fact]
    public async Task Agents_see_their_own_sales_and_earn_commission_on_collections()
    {
        var c = await SetupAsync();
        c.User.BusinessType = BusinessType.Agency;
        var mine = (await c.Sales().CreateAsync(c.Plan(c.PlotA, c.AgentId, commission: 3m))).Value;
        var other = (await c.Sales().CreateAsync(c.Plan(c.PlotB))).Value;
        c.Db.ChangeTracker.Clear();
        await c.Sales().RecordPaymentAsync(mine, new SalePaymentInput(Today, 300_000m, PaymentMethod.Cash, null, null));
        c.Db.ChangeTracker.Clear();

        var row = Assert.Single(await c.Sales().SalesCommissionAsync(2026, 10));
        Assert.Equal((300_000m, 9_000m, 3_600m), (row.Collected, row.Commission, row.AgentShare));

        c.User.UserId = "james";
        c.User.Role = BusinessRole.User;
        Assert.Equal(new[] { mine }, (await c.Sales().ListAsync(SalesListView.All, null, null, null)).Select(s => s.Id));
        Assert.Null(await c.Sales().GetAsync(other));
        Assert.False((await c.Sales().CancelAsync(mine, "test", false)).Succeeded);   // agents can't cancel
    }
}
