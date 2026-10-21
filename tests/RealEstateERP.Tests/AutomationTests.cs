using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class AutomationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);   // 11 Oct 2026 (EAT 10:00)

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, FakeEmail Email, int BusinessId, int TenantId, int LeaseId)
    {
        private SystemConfigService Config() => new(Db, new MemoryCache(new MemoryCacheOptions()));
        public TenantMessagingService Messaging() => new(Db, Email, User, Clock);
        public AutomationService Automation()
        {
            var numbers = new NumberSequenceService(Db);
            var billing = new BillingRunService(Db, numbers, new CreditService(Db, User, Config(), Clock), Clock);
            var arrears = new ArrearsService(Db, numbers, Config(), Clock, User);
            var listings = new ListingService(Db, new FakeStorage(), Config(), User, Clock);
            return new AutomationService(billing, arrears, Messaging(), listings, Db, Config(), User, Clock);
        }

        public async Task SetAsync(Action<Business> change)
        {
            var b = await Db.Businesses.SingleAsync(x => x.Id == BusinessId);
            change(b);
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public Task<int> MonthlyInvoicesAsync() => Db.Invoices.CountAsync(i => i.Kind == InvoiceKind.Monthly && i.PeriodStart == new DateOnly(2026, 10, 1));
    }

    private static async Task<Ctx> SetupAsync(string? tenantEmail = "faith@tenant.test")
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Acacia Gardens Ltd", Email = "office@acacia.test", CreditBalance = 100 };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;

        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Building };
        var unit = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 25_000m, Status = UnitStatus.Occupied };
        var tenant = new Tenant { FullName = "Faith Mutua", Email = tenantEmail };
        var lease = new Lease { LeaseNumber = "L-1", Tenant = tenant, Unit = unit, Status = LeaseStatus.Active, StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31), MonthlyRent = 25_000m, RentDueDay = 5 };
        db.AddRange(asset, unit, tenant, lease);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), new FakeEmail(), business.Id, tenant.Id, lease.Id);
    }

    [Fact]
    public async Task Invoices_once_a_month_on_or_after_the_chosen_day_and_emails_them()
    {
        var c = await SetupAsync();
        await c.SetAsync(b => { b.AutoInvoiceEnabled = true; b.AutoInvoiceDay = 15; b.EmailInvoicesToTenants = true; });

        var early = await c.Automation().RunAsync();   // 11th: before the 15th
        Assert.Equal(0, early.InvoicesCreated);
        Assert.Equal(0, await c.MonthlyInvoicesAsync());

        await c.SetAsync(b => b.AutoInvoiceDay = 5);
        var run = await c.Automation().RunAsync();
        Assert.Equal((1, 1), (run.InvoicesCreated, run.InvoicesEmailed));
        Assert.Equal(1, await c.MonthlyInvoicesAsync());
        var email = Assert.Single(c.Email.Sent);
        Assert.Equal(("faith@tenant.test", "office@acacia.test"), (email.To, email.ReplyTo));
        Assert.Contains("25,000.00", email.HtmlBody);

        c.Db.ChangeTracker.Clear();
        var again = await c.Automation().RunAsync();   // same month: nothing more
        Assert.Equal(0, again.InvoicesCreated);
        Assert.Equal(1, await c.MonthlyInvoicesAsync());
        Assert.Equal(new DateOnly(2026, 10, 1), (await c.Db.Businesses.SingleAsync()).LastAutoInvoiceMonth);
    }

    [Fact]
    public async Task Penalties_follow_the_business_grace_period()
    {
        var c = await SetupAsync();
        c.Db.Invoices.Add(new Invoice
        {
            InvoiceNumber = "INV-OLD", Kind = InvoiceKind.Monthly, TenantId = c.TenantId, LeaseId = c.LeaseId, Status = InvoiceStatus.Pending,
            InvoiceDate = new DateOnly(2026, 9, 1), DueDate = new DateOnly(2026, 9, 21), SubTotal = 25_000m, Total = 25_000m   // 20 days overdue
        });
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await c.SetAsync(b => { b.AutoPenaltiesEnabled = true; b.PenaltyGraceDays = 30; b.PenaltyPercent = 10m; });
        Assert.Equal(0, (await c.Automation().RunAsync()).PenaltiesIssued);   // still within the business's 30-day grace

        c.Db.ChangeTracker.Clear();
        await c.SetAsync(b => b.PenaltyGraceDays = 7);
        var run = await c.Automation().RunAsync();
        Assert.Equal((1, 2_500m), (run.PenaltiesIssued, run.PenaltyTotal));   // 10% of 25,000

        c.Db.ChangeTracker.Clear();
        Assert.Equal(0, (await c.Automation().RunAsync()).PenaltiesIssued);   // never twice
    }

    [Fact]
    public async Task Switched_off_does_nothing_and_the_daily_claim_happens_once()
    {
        var c = await SetupAsync();
        Assert.False((await c.Automation().RunAsync()).Ran);

        Assert.True(await c.Automation().TryClaimDailyRunAsync());
        c.Db.ChangeTracker.Clear();
        Assert.False(await c.Automation().TryClaimDailyRunAsync());   // already claimed today
    }

    [Fact]
    public async Task Receipts_are_emailed_only_when_switched_on_and_need_an_email()
    {
        var c = await SetupAsync();
        var payment = new Payment { ReceiptNumber = "RCT-1", TenantId = c.TenantId, PaymentDate = new DateOnly(2026, 10, 10), Amount = 5_000m, Method = PaymentMethod.Cash };
        c.Db.Payments.Add(payment);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        Assert.Null(await c.Messaging().AutoSendReceiptAsync(payment.Id));   // setting off
        Assert.Empty(c.Email.Sent);

        await c.SetAsync(b => b.EmailReceiptsToTenants = true);
        Assert.Contains("emailed", await c.Messaging().AutoSendReceiptAsync(payment.Id));
        Assert.Contains("RCT-1", Assert.Single(c.Email.Sent).Subject);

        var none = await SetupAsync(tenantEmail: null);
        var p2 = new Payment { ReceiptNumber = "RCT-2", TenantId = none.TenantId, PaymentDate = new DateOnly(2026, 10, 10), Amount = 1_000m, Method = PaymentMethod.Cash };
        none.Db.Payments.Add(p2);
        await none.Db.SaveChangesAsync();
        Assert.False((await none.Messaging().SendReceiptAsync(p2.Id)).Succeeded);
    }
}
