using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// Money paid back to a tenant: a deposit at the end of a lease, or an overpayment (FR-08-D).
/// An overpayment refund is drawn from a specific payment's unallocated balance (PaymentId).
/// </summary>
public class Refund : TenantEntity
{
    public string RefundNumber { get; set; } = string.Empty;
    public RefundKind Kind { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int? LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public DateOnly RefundDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
}
