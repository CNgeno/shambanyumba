using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure lease rules. No database; fully unit-tested.</summary>
public static class LeaseRules
{
    public const int MaxRentDueDay = 28;   // every month has a 28th

    public static string? ValidateTerms(DateOnly start, DateOnly? end, decimal rent, decimal deposit, int noticeDays, int dueDay)
    {
        if (end is DateOnly e && e <= start) return "The end date must be after the start date.";
        if (rent <= 0) return "Enter the monthly rent.";
        if (deposit < 0) return "The deposit can't be negative.";
        if (noticeDays is < 0 or > 365) return "The notice period must be between 0 and 365 days.";
        if (dueDay is < 1 or > MaxRentDueDay) return $"Rent due day must be between 1 and {MaxRentDueDay}.";
        return null;
    }

    public static string? ActivationError(LeaseStatus lease, UnitStatus unit) => (lease, unit) switch
    {
        (not LeaseStatus.Draft, _) => "Only draft leases can be activated.",
        (_, UnitStatus.Available or UnitStatus.Reserved) => null,
        (_, UnitStatus.Occupied) => "This unit already has an active lease.",
        (_, UnitStatus.Sold) => "This unit has been sold.",
        _ => "This unit is inactive. Make it available first."
    };

    /// <summary>
    /// Suggested early-termination penalty: rent for the days of notice NOT given.
    /// e.g. 30-day notice, notice given 10 days before leaving, KES 30,000 rent -> 20 days -> KES 20,000.
    /// </summary>
    public static decimal NoticeShortfallPenalty(decimal monthlyRent, int noticeDays, DateOnly? noticeGivenOn, DateOnly endDate)
    {
        var given = noticeGivenOn is DateOnly g ? Math.Max(0, endDate.DayNumber - g.DayNumber) : 0;
        var shortfall = Math.Max(0, noticeDays - given);
        return Math.Round(monthlyRent * shortfall / 30m, 2, MidpointRounding.AwayFromZero);
    }

    public static LeaseExpiry Expiry(LeaseStatus status, DateOnly? end, DateOnly today)
    {
        if (status != LeaseStatus.Active || end is not DateOnly e) return LeaseExpiry.None;
        var days = e.DayNumber - today.DayNumber;
        return days switch
        {
            < 0 => LeaseExpiry.Ended,
            <= 30 => LeaseExpiry.Within30,
            <= 60 => LeaseExpiry.Within60,
            <= 90 => LeaseExpiry.Within90,
            _ => LeaseExpiry.None
        };
    }

    public static TenantStatus TenantStatusFrom(IEnumerable<LeaseStatus> leases)
    {
        var list = leases.ToList();
        if (list.Contains(LeaseStatus.Active)) return TenantStatus.Active;
        if (list.Any(s => s is LeaseStatus.Expired or LeaseStatus.Terminated or LeaseStatus.Renewed)) return TenantStatus.Former;
        return TenantStatus.Prospective;
    }
}
