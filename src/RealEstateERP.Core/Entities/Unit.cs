using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-03-C. A lettable or sellable unit. Sits directly under an asset, or under a structure node.</summary>
public class Unit : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public int? StructureNodeId { get; set; }
    public StructureNode? StructureNode { get; set; }

    public string UnitNumber { get; set; } = string.Empty;
    public int? FloorNumber { get; set; }
    public UnitType Type { get; set; }
    public UnitStatus Status { get; set; } = UnitStatus.Available;

    public decimal? SizeSqm { get; set; }
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }

    public decimal MonthlyRent { get; set; }
    public decimal? SalePrice { get; set; }

    /// <summary>A plot's own title after subdivision.</summary>
    public string? TitleNumber { get; set; }

    /// <summary>Null = use the business/system default (precedence: Unit -> Lease -> Business -> System).</summary>
    public int? GracePeriodDays { get; set; }
    public decimal? LatePenaltyAmount { get; set; }

    public string? Notes { get; set; }
}
