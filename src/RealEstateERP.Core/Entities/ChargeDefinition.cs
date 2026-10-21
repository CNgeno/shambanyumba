using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-06-A. The business's library of charges (garbage, security, water, service charge...).</summary>
public class ChargeDefinition : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public ChargeType Type { get; set; }

    /// <summary>Recurring / one-time / penalty amount.</summary>
    public decimal? DefaultAmount { get; set; }

    /// <summary>Utility price per unit of measure (e.g. KES 150 per m³).</summary>
    public decimal? UnitRate { get; set; }
    public string? UnitOfMeasure { get; set; }

    /// <summary>e.g. 16 for VAT. Applied to lines created from this charge.</summary>
    public decimal TaxPercent { get; set; }

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Recurring charges: billed monthly, quarterly or annually. The amount is per billing (e.g. the quarter's amount).</summary>
    public ChargeFrequency Frequency { get; set; } = ChargeFrequency.Monthly;
    /// <summary>Quarterly/annual: a month in which it's billed (1-12). Quarterly with 1 = January, April, July, October.</summary>
    public int AnchorMonth { get; set; } = 1;
}
