using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>Money received from a buyer. Immutable like rent payments: corrected only by reversal.</summary>
public class SalePayment : TenantEntity
{
    public string ReceiptNumber { get; set; } = string.Empty;

    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Active;
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversedBy { get; set; }
    public string? ReversalReason { get; set; }

    public ICollection<SaleAllocation> Allocations { get; set; } = new List<SaleAllocation>();
}

public class SaleAllocation : TenantEntity
{
    public int SalePaymentId { get; set; }
    public SalePayment SalePayment { get; set; } = null!;

    public int InstalmentId { get; set; }
    public SaleInstalment Instalment { get; set; } = null!;

    public decimal Amount { get; set; }
    public bool IsReversed { get; set; }
}
