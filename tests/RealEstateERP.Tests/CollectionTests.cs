using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class AllocationEngineTests
{
    private static readonly OpenInvoiceItem[] Open =
    [
        new(2, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 1), 10_000m),
        new(1, new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 1), 10_000m),
        new(3, new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 1), 10_000m)
    ];

    [Fact]
    public void Pays_oldest_due_first_and_keeps_the_rest()
    {
        var (allocations, left) = AllocationEngine.OldestFirst(15_000m, Open);
        Assert.Equal(new[] { (1, 10_000m), (2, 5_000m) }, allocations.ToArray());
        Assert.Equal(0m, left);

        var (_, credit) = AllocationEngine.OldestFirst(35_000m, Open);
        Assert.Equal(5_000m, credit);
    }

    [Theory]
    [InlineData(10_000, 5_000, 5_000, true)]
    [InlineData(10_000, 6_000, 5_000, false)]   // more than the payment
    [InlineData(20_000, 12_000, 0, false)]      // more than invoice 1's balance
    public void Manual_allocations_are_checked(int amount, int toInvoice1, int toInvoice2, bool valid)
    {
        var requested = new Dictionary<int, decimal> { [1] = toInvoice1, [2] = toInvoice2 };
        var error = AllocationEngine.ValidateManual(amount, requested, Open.ToDictionary(o => o.InvoiceId, o => o.Balance));
        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData(PaymentMethod.Mpesa, " sjk4h7qx2p ", "SJK4H7QX2P", true)]
    [InlineData(PaymentMethod.Mpesa, "SJK4H7", null, false)]
    [InlineData(PaymentMethod.Cheque, "", null, false)]
    [InlineData(PaymentMethod.Cash, null, null, true)]
    public void References(PaymentMethod method, string? input, string? expected, bool ok)
    {
        var (reference, error) = AllocationEngine.NormalizeReference(method, input);
        Assert.Equal(ok, error is null);
        if (ok) Assert.Equal(expected, reference);
    }
}

public class PaymentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 11);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int TenantId, int LeaseId, int SeptId, int OctId)
    {
        public PaymentService Payments() => new(Db, new NumberSequenceService(Db), User, Clock);
        public StatementService Statements() => new(Db, Clock);
        public InvoiceService Invoices() => new(Db, new NumberSequenceService(Db), Clock);

        public PaymentInput Pay(decimal amount, string? code = null, AllocationMode mode = AllocationMode.OldestFirst, Dictionary<int, decimal>? manual = null)
            => new(TenantId, LeaseId, Today, amount, code is null ? PaymentMethod.Cash : PaymentMethod.Mpesa, code, null, null, mode, manual);

        public async Task<decimal> PaidOn(int invoiceId) { Db.ChangeTracker.Clear(); return (await Db.Invoices.SingleAsync(i => i.Id == invoiceId)).AmountPaid; }
    }

    /// <summary>A tenant with two issued invoices of KES 10,000: September (overdue) and October.</summary>
    private static async Task<Ctx> SetupAsync(int businessId = 1)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = businessId;
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Estate };
        var unit = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 10_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Mercy Achieng" };
        var lease = new Lease { LeaseNumber = "L-00001", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1), MonthlyRent = 10_000m, DepositAmount = 10_000m };
        Invoice Inv(string no, int month) => new()
        {
            InvoiceNumber = no, Kind = InvoiceKind.Monthly, Tenant = tenant, Lease = lease, Status = InvoiceStatus.Pending,
            InvoiceDate = new DateOnly(2026, month, 1), DueDate = new DateOnly(2026, month, 5), PeriodStart = new DateOnly(2026, month, 1),
            SubTotal = 10_000m, Total = 10_000m
        };
        var sept = Inv("INV-00001", 9);
        var oct = Inv("INV-00002", 10);
        db.AddRange(asset, unit, tenant, lease, sept, oct);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), tenant.Id, lease.Id, sept.Id, oct.Id);
    }

    [Fact]
    public async Task Payment_settles_the_oldest_invoice_first()
    {
        var c = await SetupAsync();
        var result = await c.Payments().RecordAsync(c.Pay(15_000m, "SJK4H7QX2P"));
        Assert.True(result.Succeeded, result.Error);

        Assert.Equal(10_000m, await c.PaidOn(c.SeptId));
        Assert.Equal(5_000m, await c.PaidOn(c.OctId));
        var balance = await c.Payments().GetTenantBalanceAsync(c.TenantId);
        Assert.Equal((5_000m, 0m), (balance.Outstanding, balance.Credit));
        Assert.Equal("RCT-00001", (await c.Db.Payments.SingleAsync()).ReceiptNumber);
    }

    [Fact]
    public async Task Overpayment_becomes_credit_that_can_be_applied_later()
    {
        var c = await SetupAsync();
        await c.Payments().RecordAsync(c.Pay(25_000m));
        Assert.Equal(5_000m, (await c.Payments().GetTenantBalanceAsync(c.TenantId)).Credit);

        c.Db.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-00003", Kind = InvoiceKind.Manual, TenantId = c.TenantId, LeaseId = c.LeaseId, Status = InvoiceStatus.Pending,
            InvoiceDate = Today, DueDate = Today.AddDays(7), SubTotal = 3_000m, Total = 3_000m
        });
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        var applied = await c.Payments().ApplyCreditAsync(c.TenantId);
        Assert.Equal(3_000m, applied.Value);
        var after = await c.Payments().GetTenantBalanceAsync(c.TenantId);
        Assert.Equal((0m, 2_000m), (after.Outstanding, after.Credit));
    }

    [Fact]
    public async Task Manual_allocation_goes_where_it_is_told()
    {
        var c = await SetupAsync();
        await c.Payments().RecordAsync(c.Pay(10_000m, mode: AllocationMode.Manual, manual: new() { [c.OctId] = 10_000m }));
        Assert.Equal(0m, await c.PaidOn(c.SeptId));
        Assert.Equal(10_000m, await c.PaidOn(c.OctId));

        var tooMuch = await c.Payments().RecordAsync(c.Pay(1_000m, mode: AllocationMode.Manual, manual: new() { [c.SeptId] = 2_000m }));
        Assert.False(tooMuch.Succeeded);
    }

    [Fact]
    public async Task An_mpesa_code_is_accepted_once_unless_reversed()
    {
        var c = await SetupAsync();
        var first = (await c.Payments().RecordAsync(c.Pay(5_000m, "SJK4H7QX2P"))).Value;
        Assert.False((await c.Payments().RecordAsync(c.Pay(5_000m, "sjk4h7qx2p"))).Succeeded);

        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Payments().ReverseAsync(first, "Wrong tenant")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Payments().RecordAsync(c.Pay(5_000m, "SJK4H7QX2P"))).Succeeded);
    }

    [Fact]
    public async Task Reversal_reopens_the_invoices_and_keeps_the_record()
    {
        var c = await SetupAsync();
        var id = (await c.Payments().RecordAsync(c.Pay(20_000m))).Value;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Payments().ReverseAsync(id, " ")).Succeeded);   // reason required
        Assert.True((await c.Payments().ReverseAsync(id, "Cheque bounced")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Payments().ReverseAsync(id, "Again")).Succeeded);

        Assert.Equal(0m, await c.PaidOn(c.SeptId));
        var payment = await c.Db.Payments.Include(p => p.Allocations).SingleAsync();
        Assert.Equal((PaymentStatus.Reversed, 20_000m), (payment.Status, payment.Amount));   // original amount untouched
        Assert.All(payment.Allocations, a => Assert.True(a.IsReversed));
    }

    [Fact]
    public async Task A_paid_invoice_cannot_be_cancelled_until_the_payment_is_reversed()
    {
        var c = await SetupAsync();
        var id = (await c.Payments().RecordAsync(c.Pay(10_000m))).Value;
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Invoices().CancelAsync(c.SeptId, "Wrong amount")).Succeeded);

        await c.Payments().ReverseAsync(id, "Recorded in error");
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Invoices().CancelAsync(c.SeptId, "Wrong amount")).Succeeded);
    }

    [Fact]
    public async Task Overpayment_refund_is_limited_and_blocks_reversal()
    {
        var c = await SetupAsync();
        var id = (await c.Payments().RecordAsync(c.Pay(23_000m))).Value;   // 3,000 unallocated
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Payments().RefundOverpaymentAsync(id, new RefundInput(Today, 4_000m, PaymentMethod.Cash, null, null))).Succeeded);
        Assert.True((await c.Payments().RefundOverpaymentAsync(id, new RefundInput(Today, 3_000m, PaymentMethod.Cash, null, null))).Succeeded);
        c.Db.ChangeTracker.Clear();

        Assert.Equal(0m, (await c.Payments().GetTenantBalanceAsync(c.TenantId)).Credit);
        Assert.False((await c.Payments().ReverseAsync(id, "test")).Succeeded);
    }

    [Fact]
    public async Task Deposit_is_refunded_only_after_the_lease_ends_and_only_what_is_due()
    {
        var c = await SetupAsync();
        var refund = new RefundInput(Today, 8_000m, PaymentMethod.Mpesa, "SKREFUND01", null);
        Assert.False((await c.Payments().RefundDepositAsync(c.LeaseId, refund)).Succeeded);   // still active

        var lease = await c.Db.Leases.SingleAsync();
        lease.Status = LeaseStatus.Terminated;
        lease.DepositDeductions = 2_000m;   // 8,000 due back
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        Assert.True((await c.Payments().RefundDepositAsync(c.LeaseId, refund)).Succeeded);
        Assert.Equal(0m, await c.Payments().GetDepositRefundableAsync(c.LeaseId));
        Assert.False((await c.Payments().RefundDepositAsync(c.LeaseId, refund with { Amount = 1m })).Succeeded);
    }

    [Fact]
    public async Task Statement_closing_balance_matches_the_account_balance()
    {
        var c = await SetupAsync();
        await c.Payments().RecordAsync(c.Pay(12_000m, "SJK4H7QX2P"));
        var bounced = (await c.Payments().RecordAsync(c.Pay(5_000m))).Value;
        c.Db.ChangeTracker.Clear();
        await c.Payments().ReverseAsync(bounced, "Bounced");
        c.Db.ChangeTracker.Clear();

        var statement = await c.Statements().GetStatementAsync(c.TenantId, null, null);
        var balance = await c.Payments().GetTenantBalanceAsync(c.TenantId);

        Assert.Equal(8_000m, statement!.ClosingBalance);   // 20,000 billed - 12,000 paid (the 5,000 was reversed)
        Assert.Equal(balance.Net, statement.ClosingBalance);
        Assert.Contains(statement.Entries, e => e.Type == StatementEntryType.Reversal && e.Debit == 5_000m);
    }

    [Fact]
    public async Task Another_business_cannot_see_or_pay_the_tenant()
    {
        var c = await SetupAsync(businessId: 1);
        var id = (await c.Payments().RecordAsync(c.Pay(5_000m))).Value;
        c.Db.ChangeTracker.Clear();

        c.User.BusinessId = 2;
        Assert.Null(await c.Payments().GetDetailsAsync(id));
        Assert.False((await c.Payments().RecordAsync(c.Pay(5_000m))).Succeeded);
        Assert.False((await c.Payments().ReverseAsync(id, "x")).Succeeded);
        Assert.Null(await c.Statements().GetStatementAsync(c.TenantId, null, null));
    }
}
