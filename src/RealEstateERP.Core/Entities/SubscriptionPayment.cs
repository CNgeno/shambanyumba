using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-02-C. A business's payment to SmartBiz, awaiting admin verification.</summary>
public class SubscriptionPayment : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public int Months { get; set; }

    /// <summary>Snapshot of plan fee x months at submission. Later fee changes don't alter it.</summary>
    public decimal Amount { get; set; }

    public SubscriptionPaymentMethod Method { get; set; }
    public string Reference { get; set; } = string.Empty;

    public string? ProofStorageKey { get; set; }
    public string? ProofFileName { get; set; }

    public PaymentVerificationStatus Status { get; set; } = PaymentVerificationStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}
