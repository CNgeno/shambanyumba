using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-15. A credit bundle paid by M-PESA or bank, verified by the platform admin (same flow as renewals).</summary>
public class CreditPurchase : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int Credits { get; set; }
    public decimal Amount { get; set; }
    public SubscriptionPaymentMethod Method { get; set; }
    public string Reference { get; set; } = string.Empty;

    public PaymentVerificationStatus Status { get; set; } = PaymentVerificationStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}
