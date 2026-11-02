using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

/// <summary>booking.shambanyumba.com guest site: search, book, deposit, hold expiry, "My booking". Runs with no business signed in.</summary>
public class GuestSiteTests
{
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static async Task<(TestDbContext Db, StaySearchService Search, BookingService Host, FakeCurrentUser User, int StayId, int BusinessId)> SetupAsync(FakeClock clock)
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Bethlehem Stays" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;
        var asset = new Asset { Name = "Bethlehem Stays", Type = AssetType.Estate, Purpose = PropertyPurpose.ShortStay, Location = "Kitengela", County = "Kajiado" };
        var published = new StayUnit { Unit = new Unit { Asset = asset, UnitNumber = "S1", Bedrooms = 1 }, Title = "1-bedroom S1", BaseNightly = 4_000, MaxGuests = 2, MinNights = 1, MaxAdvanceDays = 365, DepositPercent = 50, IsPublished = true };
        var draft = new StayUnit { Unit = new Unit { Asset = asset, UnitNumber = "S2", Bedrooms = 1 }, Title = "1-bedroom S2", BaseNightly = 4_000, MaxGuests = 2, MinNights = 1, MaxAdvanceDays = 365 };
        db.StayUnits.AddRange(published, draft);
        await db.SaveChangesAsync();
        var hostUser = user;
        var host = new BookingService(db, clock, hostUser);
        user.BusinessId = null;   // the guest site: nobody signed in
        return (db, new StaySearchService(db, clock, new FakeStorage()), host, user, published.Id, business.Id);
    }

    [Fact]
    public async Task Search_shows_published_stays_and_hides_booked_dates()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero));
        var (_, search, _, _, stay, _) = await SetupAsync(clock);

        var all = await search.SearchAsync("Kitengela", null, null, 2);
        Assert.Equal([stay], all.Select(s => s.StayUnitId).ToArray());   // the draft isn't shown

        var dated = await search.SearchAsync(null, D(11, 10), D(11, 12), 2);
        Assert.Equal(8_000m, dated.Single().Quote!.Total);

        Assert.True((await search.BookAsync(new PublicBookingInput(stay, D(11, 10), D(11, 12), 2, 0, "Faith", "0712345678", null, null))).Succeeded);
        Assert.Empty(await search.SearchAsync(null, D(11, 11), D(11, 13), 2));   // taken now
        Assert.Empty(await search.SearchAsync(null, null, null, 3));             // too many guests
    }

    [Fact]
    public async Task A_guest_booking_waits_for_the_deposit_then_confirms()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero));
        var (db, search, host, user, stay, businessId) = await SetupAsync(clock);

        var r = await search.BookAsync(new PublicBookingInput(stay, D(11, 10), D(11, 13), 2, 0, "Faith Njeri", "0712 345 678", null, "Arriving late"));
        Assert.True(r.Succeeded, r.Error);
        var b = await db.Bookings.IgnoreQueryFilters().SingleAsync();
        Assert.Equal((BookingStatus.Held, businessId, BookingChannel.Direct, 6_000m), (b.Status, b.BusinessId, b.Channel, b.DepositDue));
        Assert.Equal(3, await db.BookingNights.IgnoreQueryFilters().CountAsync());

        var clash = await search.BookAsync(new PublicBookingInput(stay, D(11, 12), D(11, 14), 1, 0, "Other", "0722000000", null, null));
        Assert.False(clash.Succeeded);

        Assert.NotNull(await search.FindAsync(r.Value!, "+254712345678"));   // any phone format
        Assert.Null(await search.FindAsync(r.Value!, "0799999999"));        // wrong phone: nothing

        user.BusinessId = businessId;   // the host records the deposit
        Assert.True((await host.RecordPaymentAsync(b.Id, StayPaymentMethod.Mpesa, 6_000, D(11, 2), "SJA1B2C3D4")).Succeeded);
        Assert.Equal(BookingStatus.Confirmed, (await db.Bookings.SingleAsync()).Status);
    }

    [Fact]
    public async Task Unpaid_holds_are_released_after_24_hours()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero));
        var (db, search, _, _, stay, _) = await SetupAsync(clock);
        Assert.True((await search.BookAsync(new PublicBookingInput(stay, D(11, 10), D(11, 12), 1, 0, "Faith", "0712345678", null, null))).Succeeded);

        Assert.Equal(0, await search.ExpireHoldsAsync());   // still within the hold
        clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal(1, await search.ExpireHoldsAsync());
        Assert.Equal(BookingStatus.Cancelled, (await db.Bookings.IgnoreQueryFilters().SingleAsync()).Status);
        Assert.Equal(0, await db.BookingNights.IgnoreQueryFilters().CountAsync(n => !n.IsDeleted));
        Assert.True((await search.BookAsync(new PublicBookingInput(stay, D(11, 10), D(11, 12), 1, 0, "Next", "0722000000", null, null))).Succeeded);
    }
}
