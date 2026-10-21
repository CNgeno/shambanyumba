using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class AgencyRulesTests
{
    [Fact]
    public void Deposit_share_of_a_payment_is_left_out()
    {
        // Invoice: rent 45,000 + deposit 45,000 = 90,000, fully paid -> 45,000 is income.
        Assert.Equal(45_000m, AgencyRules.IncomePart(90_000m, 90_000m, 45_000m));
        // Half paid -> half of it counts.
        Assert.Equal(22_500m, AgencyRules.IncomePart(45_000m, 90_000m, 45_000m));
        Assert.Equal(10_000m, AgencyRules.IncomePart(10_000m, 10_000m, 0m));
    }

    [Fact]
    public void Commission_and_net()
    {
        Assert.Equal(4_500m, AgencyRules.Commission(CommissionType.PercentOfCollections, 10m, 0, 45_000m));
        Assert.Equal(5_000m, AgencyRules.Commission(CommissionType.FixedMonthly, 0, 5_000m, 45_000m));
        Assert.Equal(38_500m, AgencyRules.NetPayable(45_000m, 4_500m, -2_000m));
        Assert.Equal(1_800m, AgencyRules.AgentShare(4_500m, 40m));
    }

    [Fact]
    public void Coverage_by_month()
    {
        var start = new DateOnly(2026, 9, 1);
        var end = new DateOnly(2026, 9, 30);
        Assert.True(AgencyRules.CoveredDuring(new DateOnly(2026, 8, 1), null, start, end));                      // added before, still covered
        Assert.False(AgencyRules.CoveredDuring(new DateOnly(2026, 10, 15), null, start, end));                   // added after the month
        Assert.True(AgencyRules.CoveredDuring(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 5), start, end));  // removed during the month
        Assert.False(AgencyRules.CoveredDuring(new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 31), start, end)); // removed before
    }

    [Fact]
    public void Contract_validation()
    {
        var d = new DateOnly(2026, 1, 1);
        Assert.Null(AgencyRules.ValidateContract(d, null, CommissionType.PercentOfCollections, 10, 0, 10));
        Assert.NotNull(AgencyRules.ValidateContract(d, null, CommissionType.PercentOfCollections, 0, 0, 10));
        Assert.NotNull(AgencyRules.ValidateContract(d, null, CommissionType.FixedMonthly, 0, 0, 10));
        Assert.NotNull(AgencyRules.ValidateContract(d, null, CommissionType.PercentOfCollections, 10, 0, 31));
    }
}

public class AgencyServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Sept1 = new(2026, 9, 1);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int OwnerId, int AgentId, int AssetId, int OtherAssetId,
        int TenantId, int LeaseA, int LeaseB)
    {
        public AgentScopeService Scope() => new(Db, User, Cache);
        public AgencyService Agency() => new(Db, new NumberSequenceService(Db), Scope(), User, Clock);
        public RemittanceService Remittances() => new(Db, new NumberSequenceService(Db), Scope(), Clock);
        public Microsoft.Extensions.Caching.Memory.MemoryCache Cache { get; } = new(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
    }

    /// <summary>Owner + agent (40%); Kilimani Heights with two leased units; a second unrelated property.</summary>
    private static async Task<Ctx> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = 1;
        user.BusinessType = BusinessType.Agency;
        var owner = new PropertyOwner { FullName = "Grace Mwende", PayoutMethod = PayoutMethod.Bank, BankName = "Equity", BankAccountNumber = "01" };
        var agent = new Agent { FullName = "James Kamau", CommissionSharePercent = 40m };
        var asset = new Asset { Name = "Kilimani Heights", Type = AssetType.Building };
        var other = new Asset { Name = "Westlands Court", Type = AssetType.Building };
        var a1 = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 45_000m, Status = UnitStatus.Occupied };
        var a2 = new Unit { Asset = asset, UnitNumber = "A2", MonthlyRent = 45_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Peter Mutiso" };
        var leaseA = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = a1, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 45_000m };
        var leaseB = new Lease { LeaseNumber = "L-2", Tenant = tenant, Unit = a2, Status = LeaseStatus.Active, StartDate = Sept1, MonthlyRent = 45_000m, DepositAmount = 45_000m };
        db.AddRange(owner, agent, asset, other, a1, a2, tenant, leaseA, leaseB);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), owner.Id, agent.Id, asset.Id, other.Id, tenant.Id, leaseA.Id, leaseB.Id);
    }

    private static ContractInput Terms(Ctx c, decimal percent = 10m)
        => new(c.OwnerId, c.AgentId, new DateOnly(2026, 1, 1), null, CommissionType.PercentOfCollections, percent, 0, 10, null);

    private static async Task<int> ActiveContractAsync(Ctx c)
    {
        var id = (await c.Agency().CreateContractAsync(Terms(c))).Value;
        Assert.True((await c.Agency().AddPropertyAsync(id, c.AssetId)).Succeeded);
        Assert.True((await c.Agency().ActivateAsync(id)).Succeeded);
        c.Db.ChangeTracker.Clear();
        return id;
    }

    /// <summary>A September invoice for the lease, paid on the given date (optionally then reversed).</summary>
    private static async Task PaidInvoiceAsync(Ctx c, int leaseId, decimal rent, decimal deposit, DateOnly paidOn, bool reversed = false)
    {
        var inv = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid():N}"[..12], Kind = InvoiceKind.Monthly, TenantId = c.TenantId, LeaseId = leaseId, Status = InvoiceStatus.Pending,
            InvoiceDate = Sept1, DueDate = Sept1.AddDays(4), PeriodStart = Sept1, PeriodEnd = new DateOnly(2026, 9, 30)
        };
        inv.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Rent, "Rent", 1, rent, 0));
        if (deposit > 0) inv.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Deposit, "Deposit", 1, deposit, 0));
        InvoiceComposer.Recalculate(inv);
        inv.AmountPaid = reversed ? 0 : inv.Total;
        var payment = new Payment
        {
            ReceiptNumber = $"R-{Guid.NewGuid():N}"[..10], TenantId = c.TenantId, PaymentDate = paidOn, Amount = inv.Total, Method = PaymentMethod.Cash,
            Status = reversed ? PaymentStatus.Reversed : PaymentStatus.Active
        };
        payment.Allocations.Add(new PaymentAllocation { Invoice = inv, Amount = inv.Total, IsReversed = reversed });
        c.Db.AddRange(inv, payment);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task A_property_can_only_be_under_one_active_contract()
    {
        var c = await SetupAsync();
        await ActiveContractAsync(c);

        var second = (await c.Agency().CreateContractAsync(Terms(c))).Value;
        Assert.False((await c.Agency().AddPropertyAsync(second, c.AssetId)).Succeeded);
        Assert.False((await c.Agency().ActivateAsync(second)).Succeeded);   // no properties
        Assert.DoesNotContain(await c.Agency().GetAvailablePropertiesAsync(), p => p.AssetId == c.AssetId);
    }

    [Fact]
    public async Task Ending_a_contract_frees_its_properties()
    {
        var c = await SetupAsync();
        var first = await ActiveContractAsync(c);
        Assert.True((await c.Agency().EndAsync(first, new DateOnly(2026, 9, 30))).Succeeded);
        c.Db.ChangeTracker.Clear();

        var second = (await c.Agency().CreateContractAsync(Terms(c))).Value;
        Assert.True((await c.Agency().AddPropertyAsync(second, c.AssetId)).Succeeded);
        Assert.True((await c.Agency().ActivateAsync(second)).Succeeded);
    }

    [Fact]
    public async Task Remittance_counts_rent_received_and_leaves_out_deposits_and_reversals()
    {
        var c = await SetupAsync();
        var contract = await ActiveContractAsync(c);
        await PaidInvoiceAsync(c, c.LeaseA, 45_000m, 0, new DateOnly(2026, 9, 6));            // counts
        await PaidInvoiceAsync(c, c.LeaseB, 45_000m, 45_000m, new DateOnly(2026, 9, 2));      // deposit half excluded
        await PaidInvoiceAsync(c, c.LeaseA, 45_000m, 0, new DateOnly(2026, 9, 20), reversed: true);   // bounced
        await PaidInvoiceAsync(c, c.LeaseA, 45_000m, 0, new DateOnly(2026, 10, 3));           // next month

        var preview = await c.Remittances().PreviewAsync(contract, 2026, 9);
        Assert.Equal(90_000m, preview!.Income);
        Assert.Equal(45_000m, preview.DepositsExcluded);
        Assert.Equal(9_000m, preview.Commission);
        Assert.Equal(81_000m, preview.NetPayable);
    }

    [Fact]
    public async Task Remittance_workflow_with_adjustments()
    {
        var c = await SetupAsync();
        var contract = await ActiveContractAsync(c);
        await PaidInvoiceAsync(c, c.LeaseA, 45_000m, 0, new DateOnly(2026, 9, 6));

        var id = (await c.Remittances().CreateAsync(contract, 2026, 9)).Value;
        Assert.False((await c.Remittances().CreateAsync(contract, 2026, 9)).Succeeded);   // one per month
        c.Db.ChangeTracker.Clear();

        Assert.True((await c.Remittances().AddAdjustmentAsync(id, "Plumbing repair A1", -3_000m)).Succeeded);
        c.Db.ChangeTracker.Clear();
        var r = (await c.Remittances().GetAsync(id))!.Remittance;
        Assert.Equal((45_000m, 4_500m, -3_000m, 37_500m), (r.GrossCollections, r.CommissionAmount, r.AdjustmentsTotal, r.NetPayable));

        Assert.False((await c.Remittances().MarkPaidAsync(id, new DateOnly(2026, 10, 9), PayoutMethod.Bank, "FT123")).Succeeded);   // not approved
        Assert.True((await c.Remittances().ApproveAsync(id)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Remittances().AddAdjustmentAsync(id, "Late", 100m)).Succeeded);   // locked after approval
        Assert.True((await c.Remittances().MarkPaidAsync(id, new DateOnly(2026, 10, 9), PayoutMethod.Bank, "FT123")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Remittances().CancelAsync(id, "test")).Succeeded);   // paid
    }

    [Fact]
    public async Task Cancelling_lets_the_month_be_prepared_again()
    {
        var c = await SetupAsync();
        var contract = await ActiveContractAsync(c);
        var id = (await c.Remittances().CreateAsync(contract, 2026, 9)).Value;
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Remittances().CancelAsync(id, "A late payment needs including")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Remittances().CreateAsync(contract, 2026, 9)).Succeeded);
    }

    [Fact]
    public async Task Agent_earns_their_share_of_the_commission()
    {
        var c = await SetupAsync();
        var contract = await ActiveContractAsync(c);
        await PaidInvoiceAsync(c, c.LeaseA, 45_000m, 0, new DateOnly(2026, 9, 6));
        await c.Remittances().CreateAsync(contract, 2026, 9);

        var row = Assert.Single(await c.Remittances().AgentCommissionsAsync(2026, 9));
        Assert.Equal(("James Kamau", 4_500m, 1_800m), (row.AgentName, row.AgencyCommission, row.AgentShare));
    }
}
