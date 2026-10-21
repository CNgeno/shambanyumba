using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum OwnerType
{
    Individual,
    Company
}

public enum PayoutMethod
{
    [Display(Name = "Bank transfer")] Bank,
    [Display(Name = "M-PESA")] Mpesa,
    Cheque
}

/// <summary>FR-10-B. How the agency earns from a management contract.</summary>
public enum CommissionType
{
    [Display(Name = "% of rent collected")] PercentOfCollections,
    [Display(Name = "Fixed monthly fee")] FixedMonthly
}

public enum ContractStatus
{
    Draft,
    Active,
    Ended
}

public enum RemittanceStatus
{
    Draft,
    Approved,
    Paid,
    Cancelled
}

public enum RemittanceLineType
{
    Collections,
    Commission,
    Adjustment
}
