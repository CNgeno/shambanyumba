using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure aging and penalty rules (FR-09-A/B). No database; fully unit-tested.</summary>
public static class ArrearsRules
{
    public static int DaysOverdue(DateOnly dueDate, DateOnly today) => Math.Max(0, today.DayNumber - dueDate.DayNumber);

    public static AgingBucket Bucket(int daysOverdue) => daysOverdue switch
    {
        <= 30 => AgingBucket.Days1To30,
        <= 60 => AgingBucket.Days31To60,
        <= 90 => AgingBucket.Days61To90,
        _ => AgingBucket.Over90
    };

    /// <summary>Grace period: the unit's own setting, else the system default.</summary>
    public static int GraceDays(int? unitGraceDays, int systemDefault) => Math.Max(0, unitGraceDays ?? systemDefault);

    /// <summary>A penalty is due once the grace period after the due date has fully passed.</summary>
    public static bool PenaltyDue(DateOnly dueDate, int graceDays, DateOnly today) => today > dueDate.AddDays(graceDays);

    /// <summary>The unit's fixed late penalty if set, otherwise a percentage of the overdue balance.</summary>
    public static decimal PenaltyAmount(decimal overdueBalance, decimal? unitFixedPenalty, decimal systemPercent)
        => unitFixedPenalty is > 0
            ? unitFixedPenalty.Value
            : Math.Round(overdueBalance * systemPercent / 100m, 2, MidpointRounding.AwayFromZero);
}
