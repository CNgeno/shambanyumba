using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>FR-06-A.</summary>
public enum ChargeType
{
    [Display(Name = "Recurring (fixed monthly)")] Recurring,
    [Display(Name = "Utility (meter-based)")] Utility,
    [Display(Name = "One-time")] OneTime,
    Penalty
}

/// <summary>
/// FR-07-E. STORED values: Draft, Pending, Cancelled, WrittenOff.
/// PartiallyPaid, Paid and Overdue are COMPUTED from AmountPaid and DueDate (BillingRules.EffectiveStatus),
/// so an invoice becomes overdue the day after its due date with no background job.
/// </summary>
public enum InvoiceStatus
{
    Draft,
    Pending,
    [Display(Name = "Partly paid")] PartiallyPaid,
    Paid,
    Overdue,
    Cancelled,
    [Display(Name = "Written off")] WrittenOff
}

public enum InvoiceKind
{
    Monthly,
    Manual,
    Final,
    [System.ComponentModel.DataAnnotations.Display(Name = "Late penalty")] Penalty
}

/// <summary>FR-07-C.</summary>
public enum InvoiceLineType
{
    Rent,
    [Display(Name = "Recurring charge")] RecurringCharge,
    Utility,
    Penalty,
    Deposit,
    [Display(Name = "Service charge")] ServiceCharge,
    Credit,
    Other
}

public enum InvoiceListView
{
    Draft,
    Unpaid,
    Overdue,
    Cancelled,
    All
}

/// <summary>How often a recurring charge is billed. Quarterly/annual charges fall due in the months set by the anchor month.</summary>
public enum ChargeFrequency
{
    Monthly,
    Quarterly,
    [System.ComponentModel.DataAnnotations.Display(Name = "Annually")] Annual
}
