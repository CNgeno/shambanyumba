using RealEstateERP.Core.Common;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// A charge every lease in this estate pays (garbage, security, water...), with this estate's amount or rate.
/// New leases in the estate get it automatically; "Apply to current leases" adds it where missing.
/// The ChargeDefinition stays the business's catalogue entry (name, type, frequency).
/// </summary>
public class PropertyCharge : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public int ChargeDefinitionId { get; set; }
    public ChargeDefinition ChargeDefinition { get; set; } = null!;

    /// <summary>Fixed charges: this estate's amount (null = the catalogue amount).</summary>
    public decimal? Amount { get; set; }
    /// <summary>Metered charges: this estate's price per unit (null = the catalogue rate).</summary>
    public decimal? UnitRate { get; set; }
}

/// <summary>A photo of the estate (gate, compound, exterior). Used on the property page and on its listings.</summary>
public class AssetPhoto : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>An interior photo of one unit (optional). Listings use these first, then the estate's photos.</summary>
public class UnitPhoto : TenantEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
