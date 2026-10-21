using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum PartnerKind
{
    Influencer,
    [Display(Name = "Business developer")] BusinessDeveloper,
    Customer,
    Other
}

public enum CommissionStatus { Owed, Paid }
