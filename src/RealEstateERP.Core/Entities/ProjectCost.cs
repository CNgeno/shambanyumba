using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// A budget estimate or an actual expense on a property (project). UnitId set = a cost of that unit only;
/// otherwise it's shared and spread over the units by the property's allocation method.
/// </summary>
public class ProjectCost : TenantEntity
{
    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public int? UnitId { get; set; }
    public Unit? Unit { get; set; }

    public CostKind Kind { get; set; }
    public CostCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    /// <summary>Required for actuals (when incurred); optional for budgets.</summary>
    public DateOnly? Date { get; set; }
    public string? Supplier { get; set; }
    public string? Reference { get; set; }
    public string? ReceiptStorageKey { get; set; }
    public string? ReceiptFileName { get; set; }
    public string? Notes { get; set; }
}
