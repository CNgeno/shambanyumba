using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum PaymentPlan
{
    [Display(Name = "Full payment")] FullPayment,
    Instalments
}

public enum InstalmentFrequency
{
    Monthly,
    Quarterly
}

/// <summary>FR-11. Reserved (unit reserved) -> Active (agreement signed, unit Sold) -> Completed (paid + handed over). Or Cancelled.</summary>
public enum SaleStatus
{
    Reserved,
    Active,
    Completed,
    Cancelled
}

/// <summary>FR-11-B transfer milestones, completed in this order.</summary>
public enum MilestoneType
{
    [Display(Name = "Sale agreement")] Agreement = 1,
    [Display(Name = "Stamp duty")] StampDuty = 2,
    [Display(Name = "Title transfer")] TitleTransfer = 3,
    Handover = 4
}

/// <summary>Computed from amount, amount paid and due date.</summary>
public enum InstalmentState
{
    Paid,
    [Display(Name = "Partly paid")] PartlyPaid,
    Due,
    Overdue
}

public enum SalesListView
{
    Open,
    Reserved,
    Active,
    Completed,
    Cancelled,
    All
}
