using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>What a property is for. Decides its page and which menus the business sees.</summary>
public enum PropertyPurpose
{
    [Display(Name = "To let")] Let,
    [Display(Name = "For sale")] Sell,
    [Display(Name = "To let and for sale")] Both
}
