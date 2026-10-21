using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Web.Models;

public sealed class CreateBusinessViewModel
{
    [Required, StringLength(200)]
    [Display(Name = "Business name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The only question about how the business works; whether properties are let or sold is set per property.</summary>
    [Display(Name = "We manage properties for other owners (agency: owner statements and commission)")]
    public bool ManagesForOthers { get; set; }

    public BusinessType? Type => ManagesForOthers ? BusinessType.Agency : BusinessType.Owner;

    [StringLength(20), Display(Name = "Referral or promo code (optional)")]
    public string? ReferralCode { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(500)]
    [Display(Name = "Physical address")]
    public string? PhysicalAddress { get; set; }

    [RegularExpression(@"^[A-Za-z]\d{9}[A-Za-z]$", ErrorMessage = "A KRA PIN looks like A123456789Z.")]
    [Display(Name = "KRA PIN")]
    public string? KraPin { get; set; }
}
