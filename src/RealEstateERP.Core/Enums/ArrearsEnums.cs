using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>FR-09-C. Where a tenant is in the collections process. Set by staff, with a note each time.</summary>
public enum CollectionStage
{
    Normal,
    [Display(Name = "In arrears")] InArrears,
    [Display(Name = "Demand served")] DemandServed,
    Legal,
    [Display(Name = "Written off")] WrittenOff
}

public enum AgingBucket
{
    [Display(Name = "1-30 days")] Days1To30,
    [Display(Name = "31-60 days")] Days31To60,
    [Display(Name = "61-90 days")] Days61To90,
    [Display(Name = "Over 90 days")] Over90
}

public enum CreditTransactionType
{
    [Display(Name = "Plan allocation")] Allocation,
    Purchase,
    Usage,
    Adjustment
}
