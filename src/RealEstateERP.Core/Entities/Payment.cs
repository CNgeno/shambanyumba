using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-08. Money received from a tenant. IMMUTABLE once saved (NFR-07): there is no update.
/// A mistake is corrected by reversing it (Status = Reversed + who/when/why); the original values stay.
/// Not linked to one invoice: PaymentAllocations split it across invoices; the rest is the tenant's credit.
/// </summary>
public class Payment : TenantEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int? LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }

    /// <summary>M-PESA code, bank reference or cheque number. Unique per method while not reversed.</summary>
    public string? Reference { get; set; }
    public string? PayerName { get; set; }
    public string? Notes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Active;

    /// <summary>Last time the receipt was emailed to the tenant.</summary>
    public DateTime? ReceiptEmailedAtUtc { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
