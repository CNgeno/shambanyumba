using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// One subscription PERIOD. Renewals add a new row; rows are never extended in place,
/// so the table is a complete history. Links point one way only: Subscription -> Business/Plan/Payment.
/// Platform-level record: not an ITenantEntity, always queried by BusinessId explicitly.
/// </summary>
public class Subscription : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public bool IsTrial { get; set; }

    /// <summary>The approved payment that created this period (null for trials). Unique: a payment applies once.</summary>
    public int? SourcePaymentId { get; set; }
    public SubscriptionPayment? SourcePayment { get; set; }
}
