using RealEstateERP.Core.Entities;

namespace RealEstateERP.Core.Common;

/// <summary>One night of a stay and the rate that applies to it.</summary>
public sealed record StayNight(DateOnly Night, decimal Rate, string Source);

/// <summary>The price of a stay: every night, cleaning, discount, total and the deposit due now. Error set when the dates aren't allowed.</summary>
public sealed record StayQuote(IReadOnlyList<StayNight> Nights, decimal NightsTotal, decimal CleaningFee, decimal Discount,
    string? DiscountLabel, decimal Total, decimal Deposit, string? Error)
{
    public int NightCount => Nights.Count;
    public static StayQuote Invalid(string error) => new([], 0, 0, 0, null, 0, 0, error);
}

/// <summary>
/// Short-stay pricing, the one place it is worked out. Each night: the season rate if a season covers it, else the
/// weekend rate on Friday and Saturday nights (if set), else the base rate. Plus the cleaning fee, minus the monthly
/// length-of-stay discount on the nights (the longest tier the stay reaches). Deposit = the deposit % of the total, rounded to shillings.
/// </summary>
public static class StayPricing
{
    public static StayQuote Quote(StayUnit stay, IEnumerable<StaySeason> seasons, DateOnly checkIn, DateOnly checkOut, DateOnly today)
        => Quote(stay, seasons, stay.Discounts, checkIn, checkOut, today);

    public static StayQuote Quote(StayUnit stay, IEnumerable<StaySeason> seasons, IEnumerable<StayDiscount> discounts, DateOnly checkIn, DateOnly checkOut, DateOnly today)
    {
        if (checkOut <= checkIn) return StayQuote.Invalid("Check-out must be after check-in.");
        if (checkIn < today) return StayQuote.Invalid("Check-in can't be in the past.");
        if (checkIn > today.AddDays(stay.MaxAdvanceDays)) return StayQuote.Invalid($"Bookings open up to {stay.MaxAdvanceDays} days ahead.");
        var count = checkOut.DayNumber - checkIn.DayNumber;
        if (count < stay.MinNights) return StayQuote.Invalid($"The minimum stay is {stay.MinNights} night{(stay.MinNights == 1 ? "" : "s")}.");
        if (stay.MaxNights is int max && count > max) return StayQuote.Invalid($"The longest stay is {max} nights.");
        if (stay.BaseNightly <= 0) return StayQuote.Invalid("This stay has no nightly rate yet.");

        var seasonList = seasons.ToList();
        var nights = new List<StayNight>(count);
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            var season = seasonList.FirstOrDefault(s => s.From <= night && night <= s.To);
            var weekend = night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
            nights.Add(season is not null ? new StayNight(night, season.Nightly, season.Name)
                : weekend && stay.WeekendNightly is decimal w ? new StayNight(night, w, "Weekend")
                : new StayNight(night, stay.BaseNightly, "Standard"));
        }

        var nightsTotal = nights.Sum(n => n.Rate);
        // Length of stay: the longest tier the stay reaches (e.g. 14+ nights beats 5+ nights).
        var tier = discounts.Where(d => d.MinNights <= count && d.Percent > 0).OrderByDescending(d => d.MinNights).FirstOrDefault();
        var (percent, label) = tier is null ? (0, (string?)null) : (tier.Percent, $"{tier.MinNights}+ nights discount ({tier.Percent}%)");
        var discount = Math.Round(nightsTotal * percent / 100m, 0, MidpointRounding.AwayFromZero);
        var total = nightsTotal + stay.CleaningFee - discount;
        var deposit = Math.Round(total * Math.Clamp(stay.DepositPercent, 0, 100) / 100m, 0, MidpointRounding.AwayFromZero);
        return new StayQuote(nights, nightsTotal, stay.CleaningFee, discount, label, total, deposit, null);
    }
}
