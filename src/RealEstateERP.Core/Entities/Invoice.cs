using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-07. Totals are always recalculated from the lines (InvoiceMath), never typed in.</summary>
public class Invoice : TenantEntity
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceKind Kind { get; set; } = InvoiceKind.Manual;

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int? LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }

    /// <summary>The month billed (monthly invoices). One monthly invoice per lease per period.</summary>
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }

    /// <summary>Stored: Draft, Pending, Cancelled or WrittenOff. See InvoiceStatus.</summary>
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal SubTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }

    /// <summary>Maintained by payment allocation (Sprint 5).</summary>
    public decimal AmountPaid { get; set; }

    public string? Notes { get; set; }
    public DateTime? IssuedAtUtc { get; set; }
    public string? StatusReason { get; set; }

    /// <summary>Late-penalty invoices point at the overdue invoice they penalise (one-way). One penalty per invoice.</summary>
    public int? PenaltyForInvoiceId { get; set; }

    /// <summary>Last time this invoice was emailed to the tenant.</summary>
    public DateTime? EmailedAtUtc { get; set; }
    public Invoice? PenaltyForInvoice { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();

    public decimal Balance => Total - AmountPaid;
}
