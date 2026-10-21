using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class PropertiesIndexViewModel
{
    public required IReadOnlyList<AssetSummary> Assets { get; init; }
    public required UnitStats Totals { get; init; }
    public required UnitUsage Usage { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class AssetFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Enter the property name."), StringLength(150)]
    [Display(Name = "Property name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Choose the property type.")]
    [Display(Name = "Type")]
    public AssetType? Type { get; set; }

    [Display(Name = "Purpose")]
    public PropertyPurpose? Purpose { get; set; }

    [Display(Name = "Stage")]
    public AssetLifecycle LifecycleState { get; set; } = AssetLifecycle.Operational;

    [StringLength(300)]
    [Display(Name = "Location", Prompt = "e.g. Ruaka, off Limuru Road")]
    public string? Location { get; set; }

    public string? County { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "Land size (acres)")]
    public decimal? LandSizeAcres { get; set; }

    [Range(0, 100_000_000_000)]
    [Display(Name = "Acquisition cost (KES)")]
    public decimal? AcquisitionCost { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Acquisition date")]
    public DateTime? AcquisitionDate { get; set; }

    [StringLength(100)]
    [Display(Name = "Title deed number")]
    public string? TitleDeedNumber { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public static IEnumerable<SelectListItem> CountyOptions => KenyaCounties.All.Select(c => new SelectListItem(c, c));

    public AssetInput ToInput() => new(Name, Type!.Value, LifecycleState, Location, County, LandSizeAcres,
        AcquisitionCost, AcquisitionDate is DateTime d ? DateOnly.FromDateTime(d) : null, TitleDeedNumber, Description, Purpose);
}

public sealed class PropertyDetailsViewModel
{
    public required AssetDetails Details { get; init; }
    public required IReadOnlyList<UnitSummary> VisibleUnits { get; init; }
    public required UnitUsage Usage { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAdmin { get; init; }
    public UnitStatus? StatusFilter { get; init; }
    public int? NodeFilter { get; init; }
}

public sealed class NodeFormInput
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";
    public StructureNodeType NodeType { get; set; } = StructureNodeType.Block;
    public int? ParentId { get; set; }
    public int SortOrder { get; set; }

    public StructureNodeInput ToInput() => new(Name, NodeType, ParentId, SortOrder);
}

public sealed record NodeOption(int Id, string Label);

public sealed class UnitFormViewModel
{
    public int? Id { get; set; }
    public int AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public IReadOnlyList<NodeOption> NodeOptions { get; set; } = [];

    [Display(Name = "Block / floor")]
    public int? StructureNodeId { get; set; }

    [Required(ErrorMessage = "Enter the unit number."), StringLength(30)]
    [Display(Name = "Unit number", Prompt = "e.g. A101")]
    public string UnitNumber { get; set; } = "";

    [Range(-5, 200)]
    [Display(Name = "Floor number")]
    public int? FloorNumber { get; set; }

    [Required(ErrorMessage = "Choose the unit type.")]
    public UnitType? Type { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "Size (m²)")]
    public decimal? SizeSqm { get; set; }

    [Range(0, 50)]
    public int? Bedrooms { get; set; }

    [Range(0, 50)]
    public int? Bathrooms { get; set; }

    [Range(0, 100_000_000)]
    [Display(Name = "Monthly rent (KES)")]
    public decimal MonthlyRent { get; set; }

    [Range(0, 100_000_000_000)]
    [Display(Name = "Sale price (KES)")]
    public decimal? SalePrice { get; set; }

    [Range(0, 60)]
    [Display(Name = "Grace period (days)")]
    public int? GracePeriodDays { get; set; }

    [Range(0, 10_000_000)]
    [Display(Name = "Late penalty (KES)")]
    public decimal? LatePenaltyAmount { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public UnitInput ToInput() => new(AssetId, StructureNodeId, UnitNumber, FloorNumber, Type!.Value, SizeSqm,
        Bedrooms, Bathrooms, MonthlyRent, SalePrice, GracePeriodDays, LatePenaltyAmount, Notes);
}

public sealed class BulkUnitsViewModel
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public IReadOnlyList<NodeOption> NodeOptions { get; set; } = [];
    public UnitUsage? Usage { get; set; }

    [Display(Name = "Block / floor")]
    public int? StructureNodeId { get; set; }

    [StringLength(20)]
    [Display(Name = "Prefix", Prompt = "e.g. A")]
    public string? Prefix { get; set; }

    [Range(0, 1_000_000)]
    [Display(Name = "First number")]
    public int StartNumber { get; set; } = 1;

    [Range(1, BulkUnitInput.MaxCount)]
    [Display(Name = "How many units")]
    public int Count { get; set; } = 10;

    [Range(-5, 200)]
    [Display(Name = "Floor number")]
    public int? FloorNumber { get; set; }

    [Required(ErrorMessage = "Choose the unit type.")]
    public UnitType? Type { get; set; } = UnitType.Apartment;

    [Range(0, 1_000_000)]
    [Display(Name = "Size (m²)")]
    public decimal? SizeSqm { get; set; }

    [Range(0, 50)]
    public int? Bedrooms { get; set; }

    [Range(0, 50)]
    public int? Bathrooms { get; set; }

    [Range(0, 100_000_000)]
    [Display(Name = "Monthly rent (KES)")]
    public decimal MonthlyRent { get; set; }

    public BulkUnitInput ToInput() => new(AssetId, StructureNodeId, Prefix?.Trim() ?? "", StartNumber, Count,
        FloorNumber, Type!.Value, SizeSqm, Bedrooms, Bathrooms, MonthlyRent);
}
