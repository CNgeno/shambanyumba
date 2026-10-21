using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record AssetInput(
    string Name,
    AssetType Type,
    AssetLifecycle LifecycleState,
    string? Location,
    string? County,
    decimal? LandSizeAcres,
    decimal? AcquisitionCost,
    DateOnly? AcquisitionDate,
    string? TitleDeedNumber,
    string? Description,
    PropertyPurpose? Purpose = null);

public sealed record StructureNodeInput(string Name, StructureNodeType NodeType, int? ParentId, int SortOrder);

public sealed record UnitInput(
    int AssetId,
    int? StructureNodeId,
    string UnitNumber,
    int? FloorNumber,
    UnitType Type,
    decimal? SizeSqm,
    int? Bedrooms,
    int? Bathrooms,
    decimal MonthlyRent,
    decimal? SalePrice,
    int? GracePeriodDays,
    decimal? LatePenaltyAmount,
    string? Notes);

public sealed record BulkUnitInput(
    int AssetId,
    int? StructureNodeId,
    string Prefix,
    int StartNumber,
    int Count,
    int? FloorNumber,
    UnitType Type,
    decimal? SizeSqm,
    int? Bedrooms,
    int? Bathrooms,
    decimal MonthlyRent,
    decimal? SalePrice = null)
{
    public const int MaxCount = 200;

    public IReadOnlyList<string> UnitNumbers()
        => Enumerable.Range(StartNumber, Math.Clamp(Count, 0, MaxCount)).Select(n => $"{Prefix}{n}").ToList();
}

public sealed record StructureNodeDto(int Id, int? ParentId, string Name, StructureNodeType NodeType, int SortOrder);

public sealed record StructureTreeRow(
    int Id, int? ParentId, string Name, StructureNodeType NodeType, int SortOrder,
    int Depth, string Path, int UnitCount, bool HasChildren);

public sealed record UnitStats(int Available, int Reserved, int Occupied, int Sold, int Inactive, decimal LettableRent, decimal OccupiedRent)
{
    public static UnitStats Empty { get; } = new(0, 0, 0, 0, 0, 0m, 0m);

    public int Total => Available + Reserved + Occupied + Sold + Inactive;

    /// <summary>Units that can be let: excludes Sold and Inactive.</summary>
    public int Lettable => Available + Reserved + Occupied;

    public decimal OccupancyPercent => Lettable == 0 ? 0m : Math.Round(Occupied * 100m / Lettable, 1);

    public int CountOf(UnitStatus status) => status switch
    {
        UnitStatus.Available => Available,
        UnitStatus.Reserved => Reserved,
        UnitStatus.Occupied => Occupied,
        UnitStatus.Sold => Sold,
        _ => Inactive
    };

    public static UnitStats From(IEnumerable<(UnitStatus Status, int Count, decimal Rent)> rows)
    {
        int a = 0, r = 0, o = 0, s = 0, i = 0;
        decimal lettable = 0, occupied = 0;
        foreach (var (status, count, rent) in rows)
        {
            switch (status)
            {
                case UnitStatus.Available: a += count; lettable += rent; break;
                case UnitStatus.Reserved: r += count; lettable += rent; break;
                case UnitStatus.Occupied: o += count; lettable += rent; occupied += rent; break;
                case UnitStatus.Sold: s += count; break;
                default: i += count; break;
            }
        }
        return new UnitStats(a, r, o, s, i, lettable, occupied);
    }

    public static UnitStats Combine(IEnumerable<UnitStats> all) => all.Aggregate(Empty, (x, y) => new UnitStats(
        x.Available + y.Available, x.Reserved + y.Reserved, x.Occupied + y.Occupied, x.Sold + y.Sold,
        x.Inactive + y.Inactive, x.LettableRent + y.LettableRent, x.OccupiedRent + y.OccupiedRent));
}

public sealed record AssetSummary(
    int Id, string Name, AssetType Type, AssetLifecycle LifecycleState, string? Location, string? County, UnitStats Stats,
    PropertyPurpose Purpose = PropertyPurpose.Let);

public sealed record UnitSummary(
    int Id, string UnitNumber, int? StructureNodeId, string? NodePath, int? FloorNumber, UnitType Type,
    UnitStatus Status, int? Bedrooms, decimal? SizeSqm, decimal MonthlyRent, decimal? SalePrice,
    int? ActiveLeaseId = null, int? TenantId = null, string? TenantName = null);

public sealed record AssetDetails(
    Asset Asset,
    IReadOnlyList<StructureTreeRow> Tree,
    IReadOnlyList<UnitSummary> Units,
    UnitStats Stats);

public sealed record PortfolioTotals(int AssetCount, UnitStats Stats);

/// <summary>Units counted against the plan limit. Limit null = unlimited.</summary>
public sealed record UnitUsage(int Used, int? Limit, string PlanName)
{
    public int? Remaining => Limit is int l ? Math.Max(0, l - Used) : null;
    public int Percent => Limit is int l && l > 0 ? Math.Min(100, Used * 100 / l) : 0;
}
