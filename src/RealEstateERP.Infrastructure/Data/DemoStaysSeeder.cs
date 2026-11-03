using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>
/// Demo short stays for reports: "Acacia Garden Stays" (Ruaka) for Acacia Gardens Ltd, with three published stays and
/// about three months of activity: completed and paid stays, guests in house, upcoming bookings with deposits, one
/// awaiting deposit, a cancellation, a no-show, cleaning tasks and monthly stay expenses. Dates follow today.
/// Adds itself once (safe to call on every start); the nightly demo reset removes it and the next start adds it again.
/// </summary>
public static class DemoStaysSeeder
{
    private const string BusinessName = "Acacia Gardens Ltd";
    private const string PropertyName = "Acacia Garden Stays";

    public static async Task SeedAsync(ApplicationDbContext db)
    {
        var business = await db.Businesses.IgnoreQueryFilters().FirstOrDefaultAsync(b => b.Name == BusinessName);
        if (business is null) return;
        var bid = business.Id;
        if (await db.Assets.IgnoreQueryFilters().AnyAsync(a => a.BusinessId == bid && a.Name == PropertyName)) return;

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now.AddHours(3));

        // ---- property, units and stays
        var asset = new Asset
        {
            BusinessId = bid, Name = PropertyName, Type = AssetType.Building, Location = "Ruaka", County = "Kiambu",
            Purpose = PropertyPurpose.ShortStay, LifecycleState = AssetLifecycle.Operational,
            Description = "Furnished short stays in a quiet, gated compound off Limuru Road, 10 minutes from Two Rivers."
        };
        var defs = new[]
        {
            (Unit: "S1", Type: UnitType.Bedsitter, Beds: 0, Title: "Cosy studio S1", Base: 3_500m, Weekend: 4_000m, Cleaning: 500m, Guests: 2),
            (Unit: "G1", Type: UnitType.Apartment, Beds: 1, Title: "Garden suite G1", Base: 5_500m, Weekend: 6_500m, Cleaning: 800m, Guests: 2),
            (Unit: "F1", Type: UnitType.Apartment, Beds: 2, Title: "Family flat F1", Base: 8_000m, Weekend: 9_500m, Cleaning: 1_000m, Guests: 5)
        };
        var stays = new List<StayUnit>();
        foreach (var d in defs)
        {
            var unit = new Unit { BusinessId = bid, Asset = asset, UnitNumber = d.Unit, Type = d.Type, Bedrooms = d.Beds, Bathrooms = 1, Status = UnitStatus.Available };
            var stay = new StayUnit
            {
                BusinessId = bid, Unit = unit, Title = d.Title, MaxGuests = d.Guests, Beds = Math.Max(1, d.Beds), Bathrooms = 1,
                Amenities = "Wi-Fi, Smart TV, Hot shower, Kitchen, Parking, Backup water",
                HouseRules = "No smoking indoors. No parties. Quiet after 10 pm.",
                Description = $"{d.Title} at Acacia Garden Stays, Ruaka: fully furnished, cleaned between every stay.",
                MinNights = 1, MaxAdvanceDays = 365, CancellationPolicy = CancellationPolicy.Moderate, DepositPercent = 50,
                BaseNightly = d.Base, WeekendNightly = d.Weekend, CleaningFee = d.Cleaning, IsPublished = true
            };
            var decYear = today.Month == 1 && today.Day <= 3 ? today.Year - 1 : today.Year;
            stay.Seasons.Add(new StaySeason { BusinessId = bid, Name = "December holidays", From = new DateOnly(decYear, 12, 20), To = new DateOnly(decYear + 1, 1, 3), Nightly = Math.Round(d.Base * 1.4m / 100m, 0) * 100m });
            stay.Discounts.Add(new StayDiscount { BusinessId = bid, MinNights = 7, Percent = 10 });
            stay.Discounts.Add(new StayDiscount { BusinessId = bid, MinNights = 28, Percent = 20 });
            stays.Add(stay);
        }
        db.StayUnits.AddRange(stays);
        await db.SaveChangesAsync();

        // ---- guests
        var guests = new[]
        {
            ("Faith Njeri", "254712345601", "faith.njeri@example.com"), ("Brian Otieno", "254722345602", null),
            ("Amina Hassan", "254733345603", "amina.h@example.com"), ("David Kiprop", "254711345604", null),
            ("Grace Wambui", "254701345605", "grace.w@example.com"), ("Peter Mwangi", "254799345606", null),
            ("Lucy Achieng", "254745345607", "lucy.a@example.com"), ("Samuel Kariuki", "254768345608", null)
        }.Select(g => new Guest { BusinessId = bid, FullName = g.Item1, Phone = g.Item2, Email = g.Item3 }).ToList();
        db.Guests.AddRange(guests);

        // ---- bookings: one after another per stay, from about 12 weeks ago to 4 weeks ahead
        var lengths = new[] { 2, 3, 1, 4, 2, 7, 3, 2, 1, 3 };
        var gaps = new[] { 1, 2, 0, 3, 1, 2, 1, 0, 2, 1 };
        var channels = new[] { BookingChannel.WalkIn, BookingChannel.Phone, BookingChannel.Airbnb, BookingChannel.Direct, BookingChannel.BookingCom };
        var number = await db.Bookings.IgnoreQueryFilters().CountAsync(b => b.BusinessId == bid);
        var code = 0;
        var made = new List<(Booking Booking, StayUnit Stay)>();
        for (var si = 0; si < stays.Count; si++)
        {
            var stay = stays[si];
            var cursor = today.AddDays(-84 + si * 2);
            var heldDone = false;
            for (var i = 0; cursor < today.AddDays(28); i++)
            {
                var checkIn = cursor;
                var checkOut = cursor.AddDays(lengths[(i + si) % lengths.Length]);
                cursor = checkOut.AddDays(gaps[(i + si * 3) % gaps.Length]);
                var quote = StayPricing.Quote(stay, stay.Seasons, stay.Discounts, checkIn, checkOut, checkIn);
                if (quote.Error is not null) continue;

                var channel = channels[(i + si) % channels.Length];
                var status = checkOut <= today ? BookingStatus.CheckedOut : checkIn <= today ? BookingStatus.CheckedIn : BookingStatus.Confirmed;
                if (status == BookingStatus.CheckedOut && i == 6) status = BookingStatus.Cancelled;
                if (status == BookingStatus.CheckedOut && i == 10 && si == 1) status = BookingStatus.NoShow;
                if (status == BookingStatus.Confirmed && !heldDone && si == 2 && channel is BookingChannel.Direct or BookingChannel.Phone or BookingChannel.WalkIn)
                {
                    status = BookingStatus.Held;   // a guest-site booking still awaiting its deposit
                    channel = BookingChannel.Direct;
                    heldDone = true;
                }

                var guest = guests[(i * 3 + si) % guests.Count];
                var booking = new Booking
                {
                    BusinessId = bid, BookingNumber = $"BK-{++number:00000}", StayUnitId = stay.Id, Guest = guest,
                    CheckIn = checkIn, CheckOut = checkOut, Nights = quote.NightCount, Adults = Math.Min(2, stay.MaxGuests), Channel = channel,
                    ExternalRef = channel is BookingChannel.Airbnb ? $"HM{1000 + number}" : channel is BookingChannel.BookingCom ? $"{4100000 + number}" : null,
                    Status = status, NightsTotal = quote.NightsTotal, CleaningFee = quote.CleaningFee, Discount = quote.Discount,
                    Total = quote.Total, DepositDue = quote.Deposit,
                    HoldExpiresUtc = status == BookingStatus.Held ? now.AddHours(20) : null,
                    CheckedInAtUtc = status is BookingStatus.CheckedIn or BookingStatus.CheckedOut ? checkIn.ToDateTime(new TimeOnly(11, 0)) : null,
                    CheckedOutAtUtc = status == BookingStatus.CheckedOut ? checkOut.ToDateTime(new TimeOnly(7, 30)) : null,
                    CancelledAtUtc = status is BookingStatus.Cancelled or BookingStatus.NoShow ? checkIn.AddDays(-2).ToDateTime(new TimeOnly(9, 0)) : null,
                    CancellationReason = status == BookingStatus.Cancelled ? "Guest changed travel plans" : status == BookingStatus.NoShow ? "Guest didn't arrive" : null
                };
                db.Bookings.Add(booking);

                // Nights: every booking that still holds its dates.
                if (status is not (BookingStatus.Cancelled or BookingStatus.NoShow))
                    foreach (var n in quote.Nights) db.BookingNights.Add(new BookingNight { BusinessId = bid, UnitId = stay.UnitId, Night = n.Night, Booking = booking });

                // Payments: completed stays paid in full; in house and upcoming with their deposit; no-shows keep the deposit.
                decimal paid = status switch
                {
                    BookingStatus.CheckedOut => booking.Total,
                    BookingStatus.CheckedIn or BookingStatus.Confirmed or BookingStatus.NoShow => booking.DepositDue,
                    _ => 0m
                };
                if (paid > 0)
                {
                    var platform = channel is BookingChannel.Airbnb or BookingChannel.BookingCom;
                    var paidOn = status == BookingStatus.CheckedOut ? checkOut : checkIn.AddDays(-3) < today ? checkIn.AddDays(-3) : today;
                    booking.AmountPaid = paid;
                    booking.Payments.Add(new BookingPayment
                    {
                        BusinessId = bid, Method = platform ? StayPaymentMethod.Bank : (i % 4 == 0 ? StayPaymentMethod.Cash : StayPaymentMethod.Mpesa),
                        Amount = paid, ReceivedOn = paidOn > today ? today : paidOn,
                        Reference = platform ? $"{channel} payout {booking.BookingNumber}" : (i % 4 == 0 ? null : $"SK{bid % 10}{++code:0000000}"),
                        ReceiptNumber = $"RC-{booking.BookingNumber}-1"
                    });
                }
                made.Add((booking, stay));
            }
        }

        await db.SaveChangesAsync();   // bookings get their ids (cleaning tasks refer to them)

        // ---- cleaning: done after every past stay, the latest per stay still to do
        foreach (var group in made.Where(m => m.Booking.Status == BookingStatus.CheckedOut).GroupBy(m => m.Stay))
        {
            var ordered = group.OrderBy(m => m.Booking.CheckOut).ToList();
            for (var k = 0; k < ordered.Count; k++)
            {
                var last = k == ordered.Count - 1;
                db.CleaningTasks.Add(new CleaningTask
                {
                    BusinessId = bid, StayUnitId = group.Key.Id, BookingId = ordered[k].Booking.Id, Due = ordered[k].Booking.CheckOut,
                    Status = last ? CleaningStatus.Pending : CleaningStatus.Done,
                    DoneAtUtc = last ? null : ordered[k].Booking.CheckOut.ToDateTime(new TimeOnly(12, 0))
                });
            }
        }

        // ---- stay expenses: the last three months
        for (var m = -2; m <= 0; m++)
        {
            var month = new DateOnly(today.Year, today.Month, 1).AddMonths(m);
            var date = m == 0 ? (today.Day > 1 ? today.AddDays(-1) : today) : month.AddDays(24);
            var platformTotal = made.Where(x => x.Booking.Channel is BookingChannel.Airbnb or BookingChannel.BookingCom
                                                && x.Booking.Status == BookingStatus.CheckedOut
                                                && x.Booking.CheckIn >= month && x.Booking.CheckIn < month.AddMonths(1))
                                    .Sum(x => x.Booking.Total);
            void Cost(CostCategory c, string what, decimal amount, string supplier)
                => db.ProjectCosts.Add(new ProjectCost { BusinessId = bid, Asset = asset, Kind = CostKind.Actual, Category = c, Description = what, Amount = amount, Date = date, Supplier = supplier });
            Cost(CostCategory.StayWages, "Cleaner and caretaker wages", 15_000, "Staff");
            Cost(CostCategory.StayLaundry, "Laundry and guest supplies", 4_500, "Ruaka Laundry Services");
            Cost(CostCategory.StayUtilities, "Electricity, water and gas", 6_200, "KPLC, Ruiru-Juja Water");
            Cost(CostCategory.StayInternet, "Wi-Fi and TV subscription", 3_000, "Safaricom Home Fibre");
            if (platformTotal > 0) Cost(CostCategory.StayCommissions, "Airbnb and Booking.com commissions", Math.Round(platformTotal * 0.15m, 0), "Airbnb, Booking.com");
            if (m == -1) Cost(CostCategory.StayRepairs, "Replaced shower head and two bulbs", 2_800, "Ruaka Hardware");
        }
        await db.SaveChangesAsync();
    }
}
