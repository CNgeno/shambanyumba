using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure function: FR-02-B lifecycle derived from dates. Trial/Active -> Grace -> Suspended.</summary>
public static class SubscriptionStatusCalculator
{
    public static SubscriptionStatus Evaluate(bool isTrial, DateTime paidThroughUtc, DateTime nowUtc, int graceDays)
    {
        if (nowUtc <= paidThroughUtc)
            return isTrial ? SubscriptionStatus.Trial : SubscriptionStatus.Active;
        if (nowUtc <= paidThroughUtc.AddDays(graceDays))
            return SubscriptionStatus.Grace;
        return SubscriptionStatus.Suspended;
    }
}
