using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Which status changes a person may make by hand. Lease-driven changes come in Sprint 3.</summary>
public static class UnitStatusRules
{
    public static string? ManualChangeError(UnitStatus from, UnitStatus to) => (from, to) switch
    {
        _ when from == to => "The unit already has that status.",
        (_, UnitStatus.Occupied) => "A unit becomes occupied when a lease is activated.",
        (UnitStatus.Occupied, _) => "An occupied unit is released by ending its lease.",
        (UnitStatus.Sold, _) => "A sold unit can't change status.",
        _ => null
    };

    public static IReadOnlyList<UnitStatus> ManualTargets(UnitStatus from)
        => Enum.GetValues<UnitStatus>().Where(to => ManualChangeError(from, to) is null).ToList();

    /// <summary>Inactive and Sold units don't count against the plan's unit limit.</summary>
    public static bool CountsTowardLimit(UnitStatus status) => status is not (UnitStatus.Inactive or UnitStatus.Sold);
}
