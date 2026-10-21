using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class KenyanPhoneTests
{
    [Theory]
    [InlineData("0712345678", "+254712345678")]
    [InlineData("0712 345 678", "+254712345678")]
    [InlineData("712345678", "+254712345678")]
    [InlineData("254712345678", "+254712345678")]
    [InlineData("+254 712 345 678", "+254712345678")]
    [InlineData("0110123456", "+254110123456")]
    [InlineData("+44 20 7946 0958", "+442079460958")]
    [InlineData("12345", null)]
    [InlineData("0201234567", null)]   // landline-style 02x isn't a mobile
    public void Normalizes(string input, string? expected) => Assert.Equal(expected, KenyanPhone.Normalize(input));

    [Fact]
    public void Displays_local_format() => Assert.Equal("0712 345 678", KenyanPhone.Display("+254712345678"));
}

public class LeaseRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    [Fact]
    public void End_must_be_after_start()
        => Assert.NotNull(LeaseRules.ValidateTerms(Today, Today, 10_000m, 0m, 30, 1));

    [Theory]
    [InlineData(30, 10, 20_000)]   // 30-day notice, 10 days given -> 20 days short -> 20/30 of 30,000
    [InlineData(30, 30, 0)]
    [InlineData(30, 45, 0)]
    [InlineData(60, null, 60_000)] // no notice at all -> 60 days -> two months
    public void Notice_shortfall_penalty(int noticeDays, int? daysGiven, decimal expected)
    {
        DateOnly? givenOn = daysGiven is int d ? Today.AddDays(-d) : null;
        Assert.Equal(expected, LeaseRules.NoticeShortfallPenalty(30_000m, noticeDays, givenOn, Today));
    }

    [Theory]
    [InlineData(-1, LeaseExpiry.Ended)]
    [InlineData(0, LeaseExpiry.Within30)]
    [InlineData(30, LeaseExpiry.Within30)]
    [InlineData(45, LeaseExpiry.Within60)]
    [InlineData(80, LeaseExpiry.Within90)]
    [InlineData(200, LeaseExpiry.None)]
    public void Expiry_buckets(int daysToEnd, LeaseExpiry expected)
        => Assert.Equal(expected, LeaseRules.Expiry(LeaseStatus.Active, Today.AddDays(daysToEnd), Today));

    [Fact]
    public void Only_active_leases_expire()
        => Assert.Equal(LeaseExpiry.None, LeaseRules.Expiry(LeaseStatus.Draft, Today.AddDays(-5), Today));
}

public class LeaseServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);   // 10:00 EAT
    private static DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime.AddHours(3));

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeClock Clock, int UnitA, int UnitB, int TenantId)
    {
        public LeaseService Leases() => new(Db, new NumberSequenceService(Db), Clock);
        public UnitService Units() => new(Db, new FakeSubscriptions(null), User);
        public TenantService Tenants() => new(Db, new FakeStorage(), new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), Clock);
        public LeaseInput Input(int unitId, DateOnly? start = null, DateOnly? end = null)
        {
            var s = start ?? Today;
            return new LeaseInput(TenantId, unitId, s, end ?? s.AddYears(1).AddDays(-1), 20_000m, 20_000m, 30, 1, null);
        }
    }

    private static async Task<Ctx> SetupAsync(int businessId = 1)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = businessId;
        var asset = new Asset { Name = "Acacia Court", Type = AssetType.Estate };
        var a = new Unit { Asset = asset, UnitNumber = "A1", MonthlyRent = 20_000m };
        var b = new Unit { Asset = asset, UnitNumber = "A2", MonthlyRent = 25_000m };
        var tenant = new Tenant { FullName = "Mercy Achieng" };
        db.AddRange(asset, a, b, tenant);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeClock(Now), a.Id, b.Id, tenant.Id);
    }

    private static async Task<UnitStatus> UnitStatusOf(Ctx c, int unitId)
        => (await c.Db.Units.AsNoTracking().SingleAsync(u => u.Id == unitId)).Status;

    [Fact]
    public async Task Activating_a_lease_occupies_the_unit()
    {
        var c = await SetupAsync();
        var result = await c.Leases().CreateAsync(c.Input(c.UnitA), activate: true);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(UnitStatus.Occupied, await UnitStatusOf(c, c.UnitA));
    }

    [Fact]
    public async Task A_unit_cannot_have_two_active_leases()
    {
        var c = await SetupAsync();
        await c.Leases().CreateAsync(c.Input(c.UnitA), activate: true);
        c.Db.ChangeTracker.Clear();

        var second = await c.Leases().CreateAsync(c.Input(c.UnitA), activate: true);

        Assert.False(second.Succeeded);
        Assert.NotEqual(0, second.Value);   // saved as a draft, id returned
        Assert.Equal(1, await c.Db.Leases.CountAsync(l => l.Status == LeaseStatus.Active));
    }

    [Fact]
    public async Task Lease_numbers_run_per_business()
    {
        var c = await SetupAsync();
        var first = (await c.Leases().CreateAsync(c.Input(c.UnitA), false)).Value;
        var second = (await c.Leases().CreateAsync(c.Input(c.UnitB), false)).Value;

        var numbers = await c.Db.Leases.Where(l => l.Id == first || l.Id == second).OrderBy(l => l.Id).Select(l => l.LeaseNumber).ToListAsync();
        Assert.Equal(new[] { "L-00001", "L-00002" }, numbers);
    }

    [Fact]
    public async Task Renewal_closes_the_old_lease_and_keeps_the_unit_occupied()
    {
        var c = await SetupAsync();
        var oldId = (await c.Leases().CreateAsync(c.Input(c.UnitA, Today.AddMonths(-12), Today.AddDays(-1)), true)).Value;
        c.Db.ChangeTracker.Clear();

        var renewal = await c.Leases().RenewAsync(oldId,
            new LeaseRenewalInput(Today, Today.AddYears(1).AddDays(-1), 22_000m, 20_000m, 30, 1, null));
        Assert.True(renewal.Succeeded, renewal.Error);
        c.Db.ChangeTracker.Clear();

        var old = await c.Db.Leases.SingleAsync(l => l.Id == oldId);
        var fresh = await c.Db.Leases.SingleAsync(l => l.Id == renewal.Value);
        Assert.Equal(LeaseStatus.Renewed, old.Status);
        Assert.Equal(Today.AddDays(-1), old.EndedOn);
        Assert.Equal(LeaseStatus.Active, fresh.Status);
        Assert.Equal(oldId, fresh.RenewedFromLeaseId);
        Assert.Equal(22_000m, fresh.MonthlyRent);
        Assert.Equal(UnitStatus.Occupied, await UnitStatusOf(c, c.UnitA));
        Assert.Equal(1, await c.Db.Leases.CountAsync(l => l.UnitId == c.UnitA && l.Status == LeaseStatus.Active));
    }

    [Fact]
    public async Task Termination_frees_the_unit_and_works_out_the_deposit_refund()
    {
        var c = await SetupAsync();
        var id = (await c.Leases().CreateAsync(c.Input(c.UnitA, Today.AddMonths(-3)), true)).Value;
        c.Db.ChangeTracker.Clear();

        var tooMuch = await c.Leases().TerminateAsync(id, new LeaseTerminationInput(Today, null, "Moving", 0m, 25_000m, null, false));
        Assert.False(tooMuch.Succeeded);   // deductions above the 20,000 deposit
        c.Db.ChangeTracker.Clear();

        var ok = await c.Leases().TerminateAsync(id, new LeaseTerminationInput(Today, Today.AddDays(-10), "Moving", 13_333.33m, 5_000m, "Repainting", false));
        Assert.True(ok.Succeeded, ok.Error);
        c.Db.ChangeTracker.Clear();

        var lease = await c.Db.Leases.SingleAsync(l => l.Id == id);
        Assert.Equal(LeaseStatus.Terminated, lease.Status);
        Assert.Equal(15_000m, lease.DepositRefundDue);
        Assert.Equal(UnitStatus.Available, await UnitStatusOf(c, c.UnitA));
    }

    [Fact]
    public async Task Ending_at_expiry_waits_for_the_end_date()
    {
        var c = await SetupAsync();
        var id = (await c.Leases().CreateAsync(c.Input(c.UnitA, Today.AddMonths(-11), Today.AddDays(10)), true)).Value;
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Leases().EndAtExpiryAsync(id, false)).Succeeded);
        c.Db.ChangeTracker.Clear();

        c.Clock.Advance(TimeSpan.FromDays(11));
        Assert.True((await c.Leases().EndAtExpiryAsync(id, makeUnitInactive: true)).Succeeded);
        Assert.Equal(UnitStatus.Inactive, await UnitStatusOf(c, c.UnitA));
    }

    [Fact]
    public async Task Only_drafts_can_be_edited()
    {
        var c = await SetupAsync();
        var id = (await c.Leases().CreateAsync(c.Input(c.UnitA), false)).Value;
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Leases().UpdateDraftAsync(id, c.Input(c.UnitA) with { MonthlyRent = 21_000m })).Succeeded);
        c.Db.ChangeTracker.Clear();

        await c.Leases().ActivateAsync(id);
        c.Db.ChangeTracker.Clear();
        Assert.False((await c.Leases().UpdateDraftAsync(id, c.Input(c.UnitA) with { MonthlyRent = 5_000m })).Succeeded);
    }

    [Fact]
    public async Task Units_and_tenants_with_lease_history_are_kept()
    {
        var c = await SetupAsync();
        var id = (await c.Leases().CreateAsync(c.Input(c.UnitA, Today.AddMonths(-3)), true)).Value;
        c.Db.ChangeTracker.Clear();
        await c.Leases().TerminateAsync(id, new LeaseTerminationInput(Today, null, "Left", 0m, 0m, null, false));
        c.Db.ChangeTracker.Clear();

        Assert.False((await c.Units().DeleteAsync(c.UnitA)).Succeeded);
        Assert.False((await c.Tenants().DeleteAsync(c.TenantId)).Succeeded);
    }

    [Fact]
    public async Task Another_business_cannot_see_tenants_or_leases()
    {
        var c = await SetupAsync(businessId: 1);
        var id = (await c.Leases().CreateAsync(c.Input(c.UnitA), true)).Value;
        c.Db.ChangeTracker.Clear();

        c.User.BusinessId = 2;
        Assert.Null(await c.Leases().GetDetailsAsync(id));
        Assert.Empty(await c.Leases().ListAsync(new LeaseListFilter(LeaseListView.All, null, null)));
        Assert.Null(await c.Tenants().GetDetailsAsync(c.TenantId));
        Assert.False((await c.Leases().ActivateAsync(id)).Succeeded);
    }

    [Fact]
    public async Task Tenant_phone_is_normalized_and_id_numbers_are_unique()
    {
        var c = await SetupAsync();
        var tenants = c.Tenants();
        TenantInput Person(string id) => new(TenantType.Individual, "Brian Kimani", IdDocumentType.NationalId, id, null,
            "0722 111 222", null, null, null, null, null, null, null, null);

        var created = await tenants.CreateAsync(Person("36603344"));
        Assert.True(created.Succeeded, created.Error);
        Assert.Equal("+254722111222", (await c.Db.Tenants.SingleAsync(t => t.Id == created.Value)).Phone);
        Assert.False((await tenants.CreateAsync(Person("36603344"))).Succeeded);
    }
}
