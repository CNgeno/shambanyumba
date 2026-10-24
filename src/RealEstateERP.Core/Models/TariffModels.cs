namespace RealEstateERP.Core.Models;

/// <summary>One band a business falls in: which plan row, its range and its monthly fee (after any agency premium).</summary>
public sealed record TariffBandLine(int PlanId, string Code, string Name, decimal From, decimal? To, decimal Fee, bool IsQuoted, int CreditsPerMonth);

/// <summary>
/// What a business pays per month, worked out from its real figures: the rent-roll band (times the agency premium for
/// agencies) plus, if it sells on instalments, the instalment-accounts band.
/// </summary>
public sealed record TariffQuote(
    decimal RentRoll, int SaleAccounts, bool IsAgency, decimal AgencyMultiplier,
    TariffBandLine? RentBand, TariffBandLine? SaleBand)
{
    public decimal MonthlyTotal => (RentBand?.Fee ?? 0) + (SaleBand?.Fee ?? 0);
    public bool IsQuoted => RentBand?.IsQuoted == true || SaleBand?.IsQuoted == true;
    /// <summary>Lite: nothing to pay (small rent roll, no instalment sales).</summary>
    public bool IsFree => !IsQuoted && MonthlyTotal == 0;
    public int CreditsPerMonth => (RentBand?.CreditsPerMonth ?? 0) + (SaleBand?.CreditsPerMonth ?? 0);
    /// <summary>The plan a payment is recorded against: the rent band, or the sales band for sellers with no rent.</summary>
    public int? PrimaryPlanId => (RentBand is { Fee: > 0 } ? RentBand : SaleBand ?? RentBand)?.PlanId;

    /// <summary>Months actually charged: paying yearly gives 2 months free (12 for the price of 10).</summary>
    public static int BilledMonths(int months) => months == 12 ? 10 : months;
    public decimal TotalFor(int months) => MonthlyTotal * BilledMonths(months);
}

/// <summary>The published tariff (for the Pricing page).</summary>
public sealed record TariffTable(IReadOnlyList<TariffBandLine> RentBands, IReadOnlyList<TariffBandLine> SaleBands, decimal AgencyMultiplier, int TrialDays);
