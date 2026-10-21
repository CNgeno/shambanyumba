using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-08-B. Part of a payment applied to one invoice. Reversed (never deleted) when its payment is reversed.</summary>
public class PaymentAllocation : TenantEntity
{
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public decimal Amount { get; set; }
    public bool IsReversed { get; set; }
}
