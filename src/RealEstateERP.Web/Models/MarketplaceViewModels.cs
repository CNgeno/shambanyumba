using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class MarketSearchViewModel
{
    public required PublicSearchResult Result { get; init; }
    public required PublicSearch Search { get; init; }
}

public sealed class MarketListingViewModel
{
    public required PublicListingDetails Details { get; init; }
    public EnquiryForm Form { get; init; } = new();
    public bool Sent { get; init; }
    /// <summary>The enquiry went straight to the owner (email confirmed recently in this browser).</summary>
    public bool Delivered { get; init; }
}

public sealed class EnquiryForm
{
    public int ListingId { get; set; }
    [Required(ErrorMessage = "Enter your name."), StringLength(150)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Enter your phone number."), StringLength(20)] public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Enter your email."), EmailAddress, StringLength(256)] public string Email { get; set; } = "";
    [StringLength(1000)] public string? Message { get; set; }
    public bool Consent { get; set; }
    /// <summary>Honeypot: hidden from people, filled in by bots.</summary>
    public string? Website { get; set; }
}

public sealed class ListingForm
{
    public int? Id { get; set; }
    [Required(ErrorMessage = "Choose the unit."), Display(Name = "Unit")] public int? UnitId { get; set; }
    public ListingType Type { get; set; } = ListingType.Rent;
    [Required(ErrorMessage = "Enter a title."), StringLength(120, MinimumLength = 10)] public string Title { get; set; } = "";
    [Required(ErrorMessage = "Describe the property."), StringLength(4000, MinimumLength = 40)] public string Description { get; set; } = "";
    [Range(1, 100_000_000_000, ErrorMessage = "Enter the price.")] public decimal Price { get; set; }

    public IReadOnlyList<ListableUnit> Units { get; set; } = [];
    public ListingInput ToInput() => new(UnitId!.Value, Type, Title, Description, Price);
}

public sealed class ListingEditViewModel
{
    public required ListingForm Form { get; init; }
    public required ListingDetails Details { get; init; }
    public required bool CanEdit { get; init; }
}
