using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>What a property is for. Decides its page and which menus the business sees.</summary>
public enum PropertyPurpose
{
    [Display(Name = "To let")] Let,
    [Display(Name = "For sale")] Sell,
    [Display(Name = "To let and for sale")] Both,
    /// <summary>Furnished units let by the night (booking.shambanyumba.com): no leases, rent or invoices.</summary>
    [Display(Name = "Short stays (nightly)")] ShortStay
}
