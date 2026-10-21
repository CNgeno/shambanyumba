using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum TenantType
{
    Individual,
    Company
}

public enum IdDocumentType
{
    [Display(Name = "National ID")] NationalId,
    Passport,
    [Display(Name = "Alien ID")] AlienId,
    [Display(Name = "Company registration")] CompanyRegistration
}

public enum TenantDocumentType
{
    [Display(Name = "National ID")] NationalId,
    Passport,
    [Display(Name = "KRA PIN certificate")] KraPin,
    [Display(Name = "Lease agreement")] LeaseAgreement,
    [Display(Name = "Payslip / bank statement")] IncomeProof,
    [Display(Name = "Company documents")] CompanyDocuments,
    Other
}

/// <summary>Computed from leases, never stored.</summary>
public enum TenantStatus
{
    Prospective,
    Active,
    Former
}

/// <summary>FR-05-C.</summary>
public enum LeaseStatus
{
    Draft,
    Active,
    Expired,
    Terminated,
    Renewed
}

/// <summary>FR-05-D expiry warnings. Computed from the end date.</summary>
public enum LeaseExpiry
{
    None,
    Within90,
    Within60,
    Within30,
    Ended
}

public enum LeaseListView
{
    Active,
    [Display(Name = "Ending soon")] EndingSoon,
    Draft,
    Ended,
    All
}
