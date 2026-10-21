using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-03-A. A property: land, building, estate, mall or commercial complex.</summary>
public class Asset : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public AssetLifecycle LifecycleState { get; set; } = AssetLifecycle.Operational;

    public string? Location { get; set; }
    public string? County { get; set; }
    public decimal? LandSizeAcres { get; set; }
    public decimal? AcquisitionCost { get; set; }
    public DateOnly? AcquisitionDate { get; set; }
    public string? TitleDeedNumber { get; set; }

    // ---- Development costing ----
    public string? SellerName { get; set; }

    /// <summary>To let, for sale, or both. Null on older rows until the startup backfill sets it (see PropertyRules.Effective).</summary>
    public PropertyPurpose? Purpose { get; set; }
    /// <summary>Budgeted land price before (or instead of) the actual AcquisitionCost.</summary>
    public decimal? EstimatedLandCost { get; set; }
    public CostAllocationMethod CostAllocation { get; set; } = CostAllocationMethod.BySize;
    /// <summary>Subdivision approval / mutation reference and date, when the mother parcel is split into plots.</summary>
    public string? SubdivisionReference { get; set; }
    public DateOnly? SubdivisionApprovedOn { get; set; }
    public string? Description { get; set; }

    public ICollection<StructureNode> Nodes { get; set; } = new List<StructureNode>();
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}
