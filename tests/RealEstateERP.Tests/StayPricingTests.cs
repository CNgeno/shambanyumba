using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;

namespace RealEstateERP.Tests;

/// <summary>booking.shambanyumba.com: the short-stay price calculator.</summary>
public class StayPricingTests
{
    private static readonly DateOnly Today = new(2026, 11, 2);   // a Monday
    private static StayUnit Stay() => new() { BaseNightly = 4_000, WeekendNightly = 5_000, CleaningFee = 1_000, MinNights = 2,
        MaxAdvanceDays = 365, DepositPercent = 50 };

    private static readonly StayDiscount[] Tiers =
    [
        new() { MinNights = 7, Percent = 10 },
        new() { MinNights = 14, Percent = 12 },
        new() { MinNights = 28, Percent = 20 }
    ];

    [Fact]
    public void Weeknights_and_weekend_nights_use_their_rates()
    {
        // Thu 5, Fri 6, Sat 7 Nov: 1 standard + 2 weekend nights
        var q = StayPricing.Quote(Stay(), [], new DateOnly(2026, 11, 5), new DateOnly(2026, 11, 8), Today);
        Assert.Null(q.Error);
        Assert.Equal(3, q.NightCount);
        Assert.Equal(14_000m, q.NightsTotal);          // 4,000 + 5,000 + 5,000
        Assert.Equal(15_000m, q.Total);                // + cleaning 1,000
        Assert.Equal(7_500m, q.Deposit);               // 50%
    }

    [Fact]
    public void A_season_overrides_weekend_and_base_rates()
    {
        var season = new StaySeason { Name = "December holidays", From = new DateOnly(2026, 12, 20), To = new DateOnly(2027, 1, 2), Nightly = 8_500 };
        var q = StayPricing.Quote(Stay(), [season], new DateOnly(2026, 12, 19), new DateOnly(2026, 12, 22), Today);
        Assert.Equal(["Weekend", "December holidays", "December holidays"], q.Nights.Select(n => n.Source).ToArray());   // Sat 19 is weekend
        Assert.Equal(22_000m, q.NightsTotal);           // 5,000 + 8,500 + 8,500
    }

    [Fact]
    public void The_longest_length_of_stay_discount_reached_applies_to_the_nights_only()
    {
        var week = StayPricing.Quote(Stay(), [], Tiers, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 16), Today);   // 7 nights
        Assert.Equal("7+ nights discount (10%)", week.DiscountLabel);
        Assert.Equal(Math.Round(week.NightsTotal * 0.10m), week.Discount);
        Assert.Equal(week.NightsTotal + 1_000 - week.Discount, week.Total);   // cleaning fee not discounted

        var tenDays = StayPricing.Quote(Stay(), [], Tiers, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 19), Today);   // 10 nights: still the 7+ tier
        Assert.Equal("7+ nights discount (10%)", tenDays.DiscountLabel);

        var fortnight = StayPricing.Quote(Stay(), [], Tiers, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 23), Today);   // 14 nights
        Assert.Equal(Math.Round(fortnight.NightsTotal * 0.12m), fortnight.Discount);

        var month = StayPricing.Quote(Stay(), [], Tiers, new DateOnly(2026, 11, 9), new DateOnly(2026, 12, 7), Today);   // 28 nights
        Assert.Equal(Math.Round(month.NightsTotal * 0.20m), month.Discount);

        var short3 = StayPricing.Quote(Stay(), [], Tiers, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 12), Today);   // 3 nights: none
        Assert.Equal(0m, short3.Discount);
    }

    [Fact]
    public void Stays_longer_than_the_maximum_are_refused()
    {
        var stay = Stay();
        stay.MaxNights = 14;
        var q = StayPricing.Quote(stay, [], new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 25), Today);   // 16 nights
        Assert.Contains("longest stay is 14 nights", q.Error);
    }

    [Theory]
    [InlineData(2026, 11, 10, 2026, 11, 11, "minimum stay")]   // 1 night, minimum 2
    [InlineData(2026, 11, 10, 2026, 11, 10, "after check-in")]
    [InlineData(2026, 11, 1, 2026, 11, 4, "past")]
    public void Dates_that_are_not_allowed_are_refused(int y1, int m1, int d1, int y2, int m2, int d2, string reason)
    {
        var q = StayPricing.Quote(Stay(), [], new DateOnly(y1, m1, d1), new DateOnly(y2, m2, d2), Today);
        Assert.Contains(reason, q.Error);
        Assert.Equal(0m, q.Total);
    }

    [Fact]
    public void Without_a_weekend_rate_every_night_is_the_base_rate()
    {
        var stay = Stay();
        stay.WeekendNightly = null;
        var q = StayPricing.Quote(stay, [], new DateOnly(2026, 11, 6), new DateOnly(2026, 11, 8), Today);
        Assert.All(q.Nights, n => Assert.Equal(4_000m, n.Rate));
    }
}
