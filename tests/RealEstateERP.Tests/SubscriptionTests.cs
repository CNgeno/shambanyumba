using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now = Now.Add(by);
}

public sealed class FakeStorage : IFileStorage
{
    public Task<string> SaveAsync(Stream content, string category, string extension, CancellationToken ct = default)
        => Task.FromResult($"{category}/fake{extension}");
    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
        => Task.FromResult<Stream?>(null);
}

public class SubscriptionStatusCalculatorTests
{
    private static readonly DateTime End = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(true, -1, SubscriptionStatus.Trial)]
    [InlineData(false, -1, SubscriptionStatus.Active)]
    [InlineData(false, 0, SubscriptionStatus.Active)]
    [InlineData(false, 2, SubscriptionStatus.Grace)]
    [InlineData(true, 3, SubscriptionStatus.Grace)]
    [InlineData(false, 4, SubscriptionStatus.Suspended)]
    public void Lifecycle_follows_dates(bool isTrial, int daysAfterEnd, SubscriptionStatus expected)
        => Assert.Equal(expected, SubscriptionStatusCalculator.Evaluate(isTrial, End, End.AddDays(daysAfterEnd), graceDays: 3));
}

public class SubscriptionServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed record Ctx(TestDbContext Db, FakeClock Clock, Business Business)
    {
        // Fresh cache each time so snapshots reflect the fake clock immediately.
        public SubscriptionService Service()
        {
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new SubscriptionService(Db, new SystemConfigService(Db, cache), new FakeStorage(), cache, Clock);
        }
    }

    private static async Task<Ctx> SetupWithTrialAsync()
    {
        var (db, _) = TestDbContext.Create();
        db.SystemConfigurations.AddRange(
            new SystemConfiguration { Key = ConfigKeys.GraceDays, Value = "3", ValueType = ConfigValueType.Integer },
            new SystemConfiguration { Key = ConfigKeys.TrialPlanCode, Value = "PRO" },
            new SystemConfiguration { Key = ConfigKeys.MaxProofSizeMb, Value = "5", ValueType = ConfigValueType.Integer });
        db.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Code = "PRO", Name = "Professional", MonthlyFee = 4_500m, UnitLimit = 100, CreditsPerMonth = 200, TrialDays = 14
        });
        var business = new Business { Name = "Test Estates", Type = BusinessType.Owner };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();

        var ctx = new Ctx(db, new FakeClock(Start), business);
        await ctx.Service().AddTrialAsync(business);
        await db.SaveChangesAsync();
        return ctx;
    }

    private static async Task<int> SubmitAsync(Ctx ctx, string reference, int months = 1)
    {
        var planId = await ctx.Db.SubscriptionPlans.Select(p => p.Id).SingleAsync();
        var result = await ctx.Service().SubmitRenewalAsync(ctx.Business.Id,
            new RenewalRequest(planId, months, SubscriptionPaymentMethod.Mpesa, reference, null));
        Assert.True(result.Succeeded, result.Error);
        return result.Value;
    }

    [Fact]
    public async Task New_business_gets_a_14_day_trial()
    {
        var ctx = await SetupWithTrialAsync();
        var snap = await ctx.Service().GetSnapshotAsync(ctx.Business.Id);

        Assert.Equal(SubscriptionStatus.Trial, snap.Status);
        Assert.True(snap.IsTrial);
        Assert.Equal(Start.UtcDateTime.AddDays(14), snap.PaidThroughUtc);
    }

    [Fact]
    public async Task Expired_trial_moves_to_grace_then_suspended()
    {
        var ctx = await SetupWithTrialAsync();

        ctx.Clock.Advance(TimeSpan.FromDays(15));
        Assert.Equal(SubscriptionStatus.Grace, (await ctx.Service().GetSnapshotAsync(ctx.Business.Id)).Status);

        ctx.Clock.Advance(TimeSpan.FromDays(3));
        var snap = await ctx.Service().GetSnapshotAsync(ctx.Business.Id);
        Assert.Equal(SubscriptionStatus.Suspended, snap.Status);
        Assert.False(snap.AllowsAccess);
    }

    [Fact]
    public async Task Paying_during_trial_queues_the_paid_period_after_the_trial()
    {
        var ctx = await SetupWithTrialAsync();
        ctx.Clock.Advance(TimeSpan.FromDays(5));

        var paymentId = await SubmitAsync(ctx, "SJK4H7QX2P");
        Assert.True((await ctx.Service().ApproveAsync(paymentId, "admin", null)).Succeeded);

        var trialEnd = Start.UtcDateTime.AddDays(14);
        var paid = await ctx.Db.Subscriptions.SingleAsync(s => !s.IsTrial);
        Assert.Equal(trialEnd, paid.PeriodStartUtc);
        Assert.Equal(trialEnd.AddMonths(1), paid.PeriodEndUtc);

        var snap = await ctx.Service().GetSnapshotAsync(ctx.Business.Id);
        Assert.Equal(SubscriptionStatus.Active, snap.Status);   // no longer "trial": payment is queued
        Assert.Equal(trialEnd.AddMonths(1), snap.PaidThroughUtc);
    }

    [Fact]
    public async Task Paying_after_suspension_starts_today()
    {
        var ctx = await SetupWithTrialAsync();
        ctx.Clock.Advance(TimeSpan.FromDays(40));

        var paymentId = await SubmitAsync(ctx, "QWE123RTY9", months: 3);
        Assert.True((await ctx.Service().ApproveAsync(paymentId, "admin", null)).Succeeded);

        var paid = await ctx.Db.Subscriptions.SingleAsync(s => !s.IsTrial);
        Assert.Equal(ctx.Clock.Now.UtcDateTime, paid.PeriodStartUtc);
        Assert.Equal(ctx.Clock.Now.UtcDateTime.AddMonths(3), paid.PeriodEndUtc);
        Assert.Equal(SubscriptionStatus.Active, (await ctx.Service().GetSnapshotAsync(ctx.Business.Id)).Status);
    }

    [Fact]
    public async Task A_payment_cannot_be_approved_twice()
    {
        var ctx = await SetupWithTrialAsync();
        var paymentId = await SubmitAsync(ctx, "ZXC987VBN1");

        Assert.True((await ctx.Service().ApproveAsync(paymentId, "admin", null)).Succeeded);
        var second = await ctx.Service().ApproveAsync(paymentId, "admin", null);

        Assert.False(second.Succeeded);
        Assert.Equal(1, await ctx.Db.Subscriptions.CountAsync(s => !s.IsTrial));
    }

    [Fact]
    public async Task The_same_reference_cannot_be_submitted_twice()
    {
        var ctx = await SetupWithTrialAsync();
        var first = await SubmitAsync(ctx, "DUP1234567");
        await ctx.Service().ApproveAsync(first, "admin", null);

        var planId = await ctx.Db.SubscriptionPlans.Select(p => p.Id).SingleAsync();
        var again = await ctx.Service().SubmitRenewalAsync(ctx.Business.Id,
            new RenewalRequest(planId, 1, SubscriptionPaymentMethod.Mpesa, "dup1234567", null));

        Assert.False(again.Succeeded);
    }

    [Fact]
    public async Task Bank_transfer_requires_proof()
    {
        var ctx = await SetupWithTrialAsync();
        var planId = await ctx.Db.SubscriptionPlans.Select(p => p.Id).SingleAsync();

        var result = await ctx.Service().SubmitRenewalAsync(ctx.Business.Id,
            new RenewalRequest(planId, 1, SubscriptionPaymentMethod.BankTransfer, "BANK000123", null));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Rejecting_requires_a_reason()
    {
        var ctx = await SetupWithTrialAsync();
        var paymentId = await SubmitAsync(ctx, "NOREASON12");

        Assert.False((await ctx.Service().RejectAsync(paymentId, "admin", "  ")).Succeeded);
        Assert.True((await ctx.Service().RejectAsync(paymentId, "admin", "Reference not found on statement")).Succeeded);
    }
}
