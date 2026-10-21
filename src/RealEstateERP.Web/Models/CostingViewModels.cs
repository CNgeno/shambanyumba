using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class CostForm
{
    public int? Id { get; set; }
    public int AssetId { get; set; }
    [Display(Name = "Unit (blank = whole project)")] public int? UnitId { get; set; }
    public CostKind Kind { get; set; } = CostKind.Actual;
    public CostCategory Category { get; set; } = CostCategory.Construction;
    [Required(ErrorMessage = "Describe the cost."), StringLength(200)] public string Description { get; set; } = "";
    [Range(0.01, 100_000_000_000, ErrorMessage = "Enter the amount.")] public decimal Amount { get; set; }
    [DataType(DataType.Date)] public DateTime? Date { get; set; }
    [StringLength(150)] public string? Supplier { get; set; }
    [StringLength(100)] public string? Reference { get; set; }
    [StringLength(500)] public string? Notes { get; set; }
    public bool HasReceipt { get; set; }

    public CostInput ToInput() => new(AssetId, UnitId, Kind, Category, Description, Amount,
        Date is DateTime d ? DateOnly.FromDateTime(d) : null, Supplier, Reference, Notes);
}

public sealed class CostsPageViewModel
{
    public required ProjectOverview Overview { get; init; }
    public required IReadOnlyList<CostLine> Lines { get; init; }
    public required CostForm Form { get; init; }
    public CostKind? Kind { get; init; }
    public CostCategory? Category { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class ProjectSetupForm
{
    public int AssetId { get; set; }
    public string AssetName { get; set; } = "";
    public AssetType AssetType { get; set; }

    [Range(0, 100_000_000_000), Display(Name = "Purchase price (KES)")] public decimal? AcquisitionCost { get; set; }
    [DataType(DataType.Date), Display(Name = "Purchase date")] public DateTime? AcquisitionDate { get; set; }
    [StringLength(150), Display(Name = "Bought from")] public string? SellerName { get; set; }
    [Range(0, 100_000_000_000), Display(Name = "Budgeted land price (KES)")] public decimal? EstimatedLandCost { get; set; }
    [StringLength(100), Display(Name = "Title / mother title number")] public string? TitleDeedNumber { get; set; }
    [Range(0.0001, 1_000_000), Display(Name = "Land size (acres)")] public decimal? LandSizeAcres { get; set; }
    [Display(Name = "Spread shared costs")] public CostAllocationMethod CostAllocation { get; set; }
    [StringLength(100), Display(Name = "Subdivision approval / mutation ref.")] public string? SubdivisionReference { get; set; }
    [DataType(DataType.Date), Display(Name = "Approved on")] public DateTime? SubdivisionApprovedOn { get; set; }

    public ProjectSetupInput ToInput() => new(AcquisitionCost, AcquisitionDate is DateTime a ? DateOnly.FromDateTime(a) : null, SellerName,
        EstimatedLandCost, TitleDeedNumber, LandSizeAcres, CostAllocation, SubdivisionReference,
        SubdivisionApprovedOn is DateTime s ? DateOnly.FromDateTime(s) : null);
}

public sealed class SetupPageViewModel
{
    public required ProjectSetupForm Form { get; init; }
    public required ProjectOverview Overview { get; init; }
    public required bool CanEdit { get; init; }
}
