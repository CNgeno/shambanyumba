using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Services;

namespace RealEstateERP.Tests;

/// <summary>booking.shambanyumba.com: bookings, blocks, cancellations, check-out and cleaning.</summary>
public class BookingServiceTests
{
    // Monday 2 November 2026, 09:00 East Africa Time
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 2, 6, 0, 0, TimeSpan.Zero));
    private static DateOnly D(int month, int day) => new(2026, month, day);

    private static async Task<(TestDbContext Db, BookingService Service, int StayId)> SetupAsync()
    {
        var (db, user) = TestDbContext.Create();
        var business = new Business { Name = "Bethlehem Estate" };
        db.Businesses.Add(business);
        await db.SaveChangesAsync();
        user.BusinessId = business.Id;
        var unit = new Unit { Asset = new Asset { Name = "Bethlehem Estate", Type = AssetType.Estate }, UnitNumber = "H3", Bedrooms = 1, MonthlyRent = 6_000 };
        var stay = new StayUnit { Unit = unit, Title = "1-bedroom at Bethlehem", BaseNightly = 4_000, CleaningFee = 500, MaxGuests = 3, MinNights = 1, MaxAdvanceDays = 365 };
        db.StayUnits.Add(stay);
        await db.SaveChangesAsync();
        return (db, new BookingService(db, Clock, user), stay.Id);
    }

    private static NewBookingInput Input(int stayId, DateOnly checkIn, DateOnly checkOut, string phone = "0712345678", int adults = 2)
        => new(stayId, checkIn, checkOut, adults, 0, "Faith Njeri", phone, null, BookingChannel.Phone, null, null);

    [Fact]
    public async Task A_booking_takes_its_nights_and_overlapping_dates_are_refused()
    {
        var (db, svc, stay) = await SetupAsync();
        var first = await svc.CreateAsync(Input(stay, D(11, 10), D(11, 13)));
        Assert.True(first.Succeeded, first.Error);
        var booking = await db.Bookings.SingleAsync();
        Assert.Equal((3, 12_500m, "BK-00001"), (booking.Nights, booking.Total, booking.BookingNumber));   // 3 x 4,000 + 500
        Assert.Equal(3, await db.BookingNights.CountAsync());

        var clash = await svc.CreateAsync(Input(stay, D(11, 12), D(11, 14), "0722000000"));
        Assert.False(clash.Succeeded);
        Assert.Contains("Not available", clash.Error);

        var backToBack = await svc.CreateAsync(Input(stay, D(11, 13), D(11, 15), "0722000000"));   // check-in on the check-out day is fine
        Assert.True(backToBack.Succeeded, backToBack.Error);
    }

    [Fact]
    public async Task Cancelling_frees_the_nights()
    {
        var (db, svc, stay) = await SetupAsync();
        var id = (await svc.CreateAsync(Input(stay, D(11, 10), D(11, 12)))).Value;
        Assert.True((await svc.CancelAsync(id, "Guest changed plans", false)).Succeeded);
        Assert.Equal(BookingStatus.Cancelled, (await db.Bookings.SingleAsync()).Status);
        Assert.True((await svc.CreateAsync(Input(stay, D(11, 10), D(11, 12), "0722000000"))).Succeeded);
    }

    [Fact]
    public async Task Blocked_dates_cannot_be_booked()
    {
        var (_, svc, stay) = await SetupAsync();
        Assert.True((await svc.BlockAsync(stay, D(11, 20), D(11, 22), "Repairs")).Succeeded);
        var r = await svc.CreateAsync(Input(stay, D(11, 21), D(11, 23)));
        Assert.False(r.Succeeded);
        Assert.Contains("Not available", r.Error);
    }

    [Fact]
    public async Task Checking_out_early_frees_the_rest_and_adds_a_cleaning_task()
    {
        var (db, svc, stay) = await SetupAsync();
        var id = (await svc.CreateAsync(Input(stay, D(11, 2), D(11, 6)))).Value;   // today to Friday
        Assert.True((await svc.CheckInAsync(id)).Succeeded);
        Assert.True((await svc.CheckOutAsync(id)).Succeeded);                     // leaves the same day
        Assert.Equal(0, await db.BookingNights.CountAsync());                      // nights from today onwards freed
        var task = await db.CleaningTasks.SingleAsync();
        Assert.Equal((CleaningStatus.Pending, D(11, 2)), (task.Status, task.Due));
    }

    [Fact]
    public async Task Too_many_guests_are_refused()
    {
        var (_, svc, stay) = await SetupAsync();
        var r = await svc.CreateAsync(Input(stay, D(11, 10), D(11, 11), adults: 4));
        Assert.False(r.Succeeded);
        Assert.Contains("at most 3 guests", r.Error);
    }

    [Fact]
    public async Task Payments_reduce_the_balance_and_each_MPESA_code_counts_once()
    {
        var (db, svc, stay) = await SetupAsync();
        var id = (await svc.CreateAsync(Input(stay, D(11, 10), D(11, 13)))).Value;   // total 12,500

        var deposit = await svc.RecordPaymentAsync(id, StayPaymentMethod.Mpesa, 6_000, D(11, 2), "sja9x8y7z6");
        Assert.True(deposit.Succeeded, deposit.Error);
        var booking = await db.Bookings.Include(b => b.Payments).SingleAsync();
        Assert.Equal((6_000m, 6_500m), (booking.AmountPaid, booking.Balance));
        Assert.Equal(("SJA9X8Y7Z6", "RC-BK-00001-1"), (booking.Payments.Single().Reference, booking.Payments.Single().ReceiptNumber));

        Assert.Contains("already been recorded", (await svc.RecordPaymentAsync(id, StayPaymentMethod.Mpesa, 1_000, D(11, 2), "SJA9X8Y7Z6")).Error);
        Assert.Contains("more than the balance", (await svc.RecordPaymentAsync(id, StayPaymentMethod.Cash, 7_000, D(11, 2), null)).Error);
        Assert.Contains("10-character M-PESA code", (await svc.RecordPaymentAsync(id, StayPaymentMethod.Mpesa, 1_000, D(11, 2), "ABC")).Error);

        Assert.True((await svc.RecordPaymentAsync(id, StayPaymentMethod.Cash, 6_500, D(11, 2), null)).Succeeded);   // the rest in cash
        Assert.Equal(0m, (await db.Bookings.SingleAsync()).Balance);
    }

    [Fact]
    public void Stay_costs_are_kept_apart_from_rental_and_development_costs()
    {
        Assert.True(RealEstateERP.Core.Common.CostingRules.IsStayCost(CostCategory.StayWages));
        Assert.False(RealEstateERP.Core.Common.CostingRules.IsRunningCost(CostCategory.StayWages));
        Assert.False(RealEstateERP.Core.Common.CostingRules.IsDevelopmentCost(CostCategory.StayWages));
        Assert.True(RealEstateERP.Core.Common.CostingRules.IsRunningCost(CostCategory.Repairs));
        Assert.Equal(CostGroup.Stays, RealEstateERP.Core.Common.CostingRules.Group(CostCategory.StayCommissions));
    }
}
