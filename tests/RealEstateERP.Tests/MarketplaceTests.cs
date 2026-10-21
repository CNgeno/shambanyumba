using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

public class MarketplaceRulesTests
{
    [Fact]
    public void Suggested_titles_and_slugs()
    {
        Assert.Equal("2-bedroom apartment in Ruaka to let", MarketplaceRules.SuggestTitle(ListingType.Rent, UnitType.Apartment, 2, null, "Ruaka, off Limuru Road"));
        Assert.Equal("450sqm plot in Kitengela for sale", MarketplaceRules.SuggestTitle(ListingType.Sale, UnitType.Plot, null, 450, "Kitengela"));
        Assert.Equal("2-bedroom-apartment-in-ruaka-acacia-court-a-3", MarketplaceRules.Slug("2-Bedroom apartment in Ruaka, Acacia Court A-3"));
    }

    [Fact]
    public void Listing_validation_and_refund_rules()
    {
        Assert.NotNull(MarketplaceRules.ValidateListing("Flat", new string('x', 50), 1000));
        Assert.NotNull(MarketplaceRules.ValidateListing("A good flat in Ruaka", "short", 1000));
        Assert.Null(MarketplaceRules.ValidateListing("A good flat in Ruaka", new string('x', 50), 1000));

        var now = new DateTime(2026, 10, 11, 8, 0, 0, DateTimeKind.Utc);
        Assert.Null(MarketplaceRules.RefundError(LeadStatus.Unlocked, now.AddDays(-2), false, now, 7, 0, 5));
        Assert.NotNull(MarketplaceRules.RefundError(LeadStatus.Unlocked, now.AddDays(-8), false, now, 7, 0, 5));   // too late
        Assert.NotNull(MarketplaceRules.RefundError(LeadStatus.Unlocked, now.AddDays(-1), true, now, 7, 0, 5));    // repeat leads were free
        Assert.NotNull(MarketplaceRules.RefundError(LeadStatus.Unlocked, now.AddDays(-1), false, now, 7, 5, 5));   // monthly limit
    }
}

public class MarketplaceServiceTests
{
    private const string VerifyUrl = "https://smartbiz.test/properties/verify?token={0}";
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 7, 0, 0, TimeSpan.Zero);

    private sealed record Ctx(TestDbContext Db, FakeCurrentUser User, FakeEmail Email, FakeClock Clock, int Biz1, int Biz2, int LiveA, int LiveD, int LetB, int DraftC)
    {
        public MarketplaceService Market() => new(Db, Email, new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), Clock);
        public ListingService Listings() => new(Db, new FakeStorage(), new SystemConfigService(Db, new MemoryCache(new MemoryCacheOptions())), User, Clock);

        public async Task<int> EnquireAndVerifyAsync(int listingId, string email, string phone = "0712345678")
        {
            Assert.True((await Market().SubmitEnquiryAsync(new EnquiryInput(listingId, "Faith Wanjiru", phone, email, "Can I view it?", true), VerifyUrl, "https://x/leads")).Succeeded);
            var token = Email.LastToken();
            Db.ChangeTracker.Clear();
            Assert.True((await Market().VerifyAsync(token, "https://x/leads")).Verified);
            Db.ChangeTracker.Clear();
            return await Db.ListingLeads.OrderByDescending(l => l.Id).Select(l => l.Id).FirstAsync();
        }

        public async Task<int> BalanceAsync(int businessId) => (await Db.Businesses.AsNoTracking().SingleAsync(b => b.Id == businessId)).CreditBalance;
    }

    /// <summary>Two businesses. Live: A (biz 1), D (biz 2). Not live: B (published but unit let), C (draft).</summary>
    private static async Task<Ctx> SetupAsync(int biz1Credits = 25)
    {
        var (db, user) = TestDbContext.Create();
        user.BusinessId = null;   // public visitor
        var b1 = new Business { Name = "Acacia Gardens Ltd", CreditBalance = biz1Credits };
        var b2 = new Business { Name = "Savanna Homes", CreditBalance = 100 };
        db.Businesses.AddRange(b1, b2);
        await db.SaveChangesAsync();
        // Listings show publicly only while the business is subscribed.
        db.Subscriptions.AddRange(
            new Subscription { BusinessId = b1.Id, PlanId = 1, PeriodStartUtc = new DateTime(2020, 1, 1), PeriodEndUtc = new DateTime(2099, 1, 1) },
            new Subscription { BusinessId = b2.Id, PlanId = 1, PeriodStartUtc = new DateTime(2020, 1, 1), PeriodEndUtc = new DateTime(2099, 1, 1) });
        await db.SaveChangesAsync();

        Listing Make(int biz, string assetName, UnitStatus unitStatus, ListingStatus status, ListingType type = ListingType.Rent)
        {
            var asset = new Asset { BusinessId = biz, Name = assetName, Type = AssetType.Building, County = "Kiambu", Location = "Ruaka" };
            var unit = new Unit { BusinessId = biz, Asset = asset, UnitNumber = "A1", Type = UnitType.Apartment, Bedrooms = 2, MonthlyRent = 30_000m, Status = unitStatus };
            return new Listing { BusinessId = biz, Unit = unit, Type = type, Title = $"2-bedroom apartment at {assetName}", Description = new string('x', 60), Price = 30_000m,
                Status = status, PublishedAtUtc = status == ListingStatus.Published ? Now.UtcDateTime : null };
        }
        var a = Make(b1.Id, "Acacia Court", UnitStatus.Available, ListingStatus.Published);
        var b = Make(b2.Id, "Let Towers", UnitStatus.Occupied, ListingStatus.Published);
        var c = Make(b2.Id, "Draft House", UnitStatus.Available, ListingStatus.Draft);
        var d = Make(b2.Id, "Savanna Heights", UnitStatus.Available, ListingStatus.Published, ListingType.Sale);
        db.Listings.AddRange(a, b, c, d);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Ctx(db, user, new FakeEmail(), new FakeClock(Now), b1.Id, b2.Id, a.Id, d.Id, b.Id, c.Id);
    }

    [Fact]
    public async Task Public_search_shows_only_live_listings_from_every_business()
    {
        var c = await SetupAsync();
        var result = await c.Market().SearchAsync(new PublicSearch(null, null, null, null, null, null, null));
        Assert.Equal(new[] { c.LiveA, c.LiveD }.OrderBy(x => x), result.Items.Select(i => i.Id).OrderBy(x => x));
        Assert.Contains(result.Items, i => i.BusinessName == "Savanna Homes");

        var sales = await c.Market().SearchAsync(new PublicSearch(ListingType.Sale, null, null, null, null, null, null));
        Assert.Equal(new[] { c.LiveD }, sales.Items.Select(i => i.Id));
        Assert.Null(await c.Market().GetAsync(c.LetB, countView: false));     // unit is let
        Assert.Null(await c.Market().GetAsync(c.DraftC, countView: false));   // not published
    }

    [Fact]
    public async Task A_lead_is_charged_only_once_confirmed_and_repeats_are_free()
    {
        var c = await SetupAsync(biz1Credits: 25);
        Assert.True((await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveA, "Faith Wanjiru", "0712345678", "faith@x.test", null, true), VerifyUrl, "")).Succeeded);
        Assert.Equal(25, await c.BalanceAsync(c.Biz1));   // nothing charged before confirmation
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Market().VerifyAsync(c.Email.LastToken(), "")).Verified);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(15, await c.BalanceAsync(c.Biz1));

        var repeat = await c.EnquireAndVerifyAsync(c.LiveA, "FAITH@x.test");   // same person again
        var lead = await c.Db.ListingLeads.SingleAsync(l => l.Id == repeat);
        Assert.True(lead.IsRepeat);
        Assert.Equal((LeadStatus.Unlocked, 0), (lead.Status, lead.CreditsCharged));
        Assert.Equal(15, await c.BalanceAsync(c.Biz1));
    }

    [Fact]
    public async Task Without_credits_a_lead_waits_until_the_business_tops_up()
    {
        var c = await SetupAsync(biz1Credits: 5);
        var id = await c.EnquireAndVerifyAsync(c.LiveA, "brian@x.test", "0722000111");
        Assert.Equal(LeadStatus.AwaitingCredits, (await c.Db.ListingLeads.SingleAsync(l => l.Id == id)).Status);

        c.User.BusinessId = c.Biz1;   // the business signs in
        var overview = await c.Listings().GetLeadsAsync(null);
        var row = Assert.Single(overview.Leads);
        Assert.False(row.ContactVisible);
        Assert.Null(row.Phone);
        Assert.False((await c.Listings().UnlockAsync(id)).Succeeded);   // still only 5 credits

        var biz = await c.Db.Businesses.SingleAsync(b => b.Id == c.Biz1);
        biz.CreditBalance = 50;
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();
        Assert.True((await c.Listings().UnlockAsync(id)).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(40, await c.BalanceAsync(c.Biz1));
        Assert.Equal("+254722000111", Assert.Single((await c.Listings().GetLeadsAsync(null)).Leads).Phone);
    }

    [Fact]
    public async Task A_fake_lead_can_be_refunded_once_and_unconfirmed_ones_never_show()
    {
        var c = await SetupAsync(biz1Credits: 25);
        var id = await c.EnquireAndVerifyAsync(c.LiveA, "spam@x.test");
        await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveA, "Never Confirms", "0733000222", "ghost@x.test", null, true), VerifyUrl, "");
        c.Db.ChangeTracker.Clear();

        c.User.BusinessId = c.Biz1;
        Assert.Single((await c.Listings().GetLeadsAsync(null)).Leads);   // the unconfirmed one isn't there
        Assert.True((await c.Listings().RefundAsync(id, "Number doesn't exist")).Succeeded);
        c.Db.ChangeTracker.Clear();
        Assert.Equal(25, await c.BalanceAsync(c.Biz1));
        Assert.False((await c.Listings().RefundAsync(id, "again")).Succeeded);
    }

    [Fact]
    public async Task One_open_listing_per_unit_and_type()
    {
        var c = await SetupAsync();
        c.User.BusinessId = c.Biz1;
        var unitId = (await c.Db.Listings.AsNoTracking().SingleAsync(l => l.Id == c.LiveA)).UnitId;
        var input = new ListingInput(unitId, ListingType.Rent, "Another 2-bedroom apartment", new string('y', 60), 31_000m);
        Assert.False((await c.Listings().CreateAsync(input)).Succeeded);
        Assert.True((await c.Listings().CreateAsync(input with { Type = ListingType.Sale, Price = 6_500_000m })).Succeeded);   // selling it too is fine
    }

    [Fact]
    public async Task Enquiries_need_consent_and_a_valid_phone()
    {
        var c = await SetupAsync();
        Assert.False((await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveA, "Faith", "0712345678", "f@x.test", null, false), VerifyUrl, "")).Succeeded);
        Assert.False((await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveA, "Faith", "12345", "f@x.test", null, true), VerifyUrl, "")).Succeeded);
        Assert.False((await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LetB, "Faith", "0712345678", "f@x.test", null, true), VerifyUrl, "")).Succeeded);   // not live
    }

    [Fact]
    public async Task One_confirmation_covers_the_same_owners_other_properties_for_10_days()
    {
        var c = await SetupAsync(biz1Credits: 100);
        var asset = new Asset { BusinessId = c.Biz1, Name = "Acacia Annex", Type = AssetType.Building, County = "Kiambu", Location = "Ruaka" };
        var unit = new Unit { BusinessId = c.Biz1, Asset = asset, UnitNumber = "B1", Type = UnitType.Apartment, Bedrooms = 1, MonthlyRent = 20_000m, Status = UnitStatus.Available };
        var other = new Listing { BusinessId = c.Biz1, Unit = unit, Type = ListingType.Rent, Title = "1-bedroom at Acacia Annex", Description = new string('x', 60),
            Price = 20_000m, Status = ListingStatus.Published, PublishedAtUtc = Now.UtcDateTime };
        c.Db.Listings.Add(other);
        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();

        await c.EnquireAndVerifyAsync(c.LiveA, "faith@x.test");   // first lead for this owner: 10 credits
        Assert.Equal(90, (await c.Db.Businesses.AsNoTracking().SingleAsync(b => b.Id == c.Biz1)).CreditBalance);

        // Same owner, another property, within 10 days, from the browser that confirmed: delivered now and free.
        var sameOwner = await c.Market().SubmitEnquiryAsync(new EnquiryInput(other.Id, "Faith Wanjiru", "0712345678", "faith@x.test", null, true, EmailConfirmedRecently: true), VerifyUrl, "");
        Assert.True(sameOwner.Value);
        var lead = await c.Db.ListingLeads.AsNoTracking().SingleAsync(l => l.ListingId == other.Id);
        Assert.Equal((LeadStatus.Unlocked, true), (lead.Status, lead.IsRepeat));
        Assert.Equal(90, (await c.Db.Businesses.AsNoTracking().SingleAsync(b => b.Id == c.Biz1)).CreditBalance);

        // Another owner: delivered straight away too, but that owner is charged.
        var otherOwner = await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveD, "Faith Wanjiru", "0712345678", "faith@x.test", null, true, EmailConfirmedRecently: true), VerifyUrl, "");
        Assert.True(otherOwner.Value);
        Assert.Equal(90, (await c.Db.Businesses.AsNoTracking().SingleAsync(b => b.Id == c.Biz2)).CreditBalance);   // 100 - 10
    }

    [Fact]
    public async Task A_browser_claim_without_a_recent_confirmation_still_sends_the_email()
    {
        var c = await SetupAsync();
        var result = await c.Market().SubmitEnquiryAsync(new EnquiryInput(c.LiveA, "Faith", "0712345678", "new@x.test", null, true, EmailConfirmedRecently: true), VerifyUrl, "");
        Assert.True(result.Succeeded);
        Assert.False(result.Value);   // the server has no confirmation for this email: normal email step
        Assert.Equal(LeadStatus.Unverified, (await c.Db.ListingLeads.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public void Credits_never_go_negative()
    {
        var business = new Business { Name = "Acacia", CreditBalance = 5 };
        Assert.Throws<InvalidOperationException>(() => CreditLedger.Post(business, -10, CreditTransactionType.Usage, "Lead"));
        Assert.Equal(5, business.CreditBalance);   // unchanged
        CreditLedger.Post(business, -5, CreditTransactionType.Usage, "PDF");
        Assert.Equal(0, business.CreditBalance);   // down to zero is fine
    }
}
