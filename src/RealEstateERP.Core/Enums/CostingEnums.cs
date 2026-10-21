using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum CostKind
{
    Budget,
    Actual
}

/// <summary>The land price itself lives on the property (purchase details); everything else is a ledger line.</summary>
public enum CostCategory
{
    [Display(Name = "Purchase costs")] PurchaseCosts = 1,     // stamp duty, legal, valuation, search, agent on purchase
    Construction = 10,
    Infrastructure = 11,                                       // roads, water, power, drainage
    [Display(Name = "Professional fees")] ProfessionalFees = 12,
    Approvals = 13,                                            // county permits, NEMA, NCA
    Subdivision = 20,                                          // survey, mutation, beacons, LCB, titles
    Marketing = 30,
    [Display(Name = "Finance costs")] Finance = 31,
    [Display(Name = "Holding costs")] Holding = 32,            // rates, security, caretaker before completion
    Other = 33,
    // Running costs (40+): the owner's own costs of a property to let. Never billed to tenants; they reduce rental income.
    [Display(Name = "Agent and management fees")] Letting = 40,
    [Display(Name = "Repairs and maintenance")] Repairs = 41,
    [Display(Name = "Renovation")] Renovation = 42,
    [Display(Name = "Cleaning")] Cleaning = 43,
    [Display(Name = "Advertising")] Advertising = 44,
    [Display(Name = "Utilities paid by the owner")] OwnerUtilities = 45,
    [Display(Name = "Rates and insurance")] RatesInsurance = 46,
    [Display(Name = "Security and caretaker")] Security = 47,
    [Display(Name = "Other running costs")] OtherRunning = 48
}

public enum CostGroup
{
    Purchase,
    Development,
    Subdivision,
    Other,
    [Display(Name = "Running costs")] Letting
}

/// <summary>How shared project costs are spread over units.</summary>
public enum CostAllocationMethod
{
    [Display(Name = "By size (sqm)")] BySize,
    [Display(Name = "Equally per unit")] Equal,
    [Display(Name = "By sale value")] ByValue
}

public enum UnitRevenueMode
{
    Sale,
    Let,
    None
}
