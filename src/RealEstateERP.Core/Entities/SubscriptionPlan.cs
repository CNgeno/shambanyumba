using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>FR-02-A. Platform-level (not tenant-scoped). Edited by the platform admin, never hardcoded.</summary>
public class SubscriptionPlan : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Null = unlimited.</summary>
    public int? UnitLimit { get; set; }
    public decimal MonthlyFee { get; set; }
    public int CreditsPerMonth { get; set; }
    /// <summary>Agency features on this plan. Null = default by code (Agent and Enterprise include them).</summary>
    public bool? IncludesAgency { get; set; }
    public int TrialDays { get; set; } = 14;

    /// <summary>Null = offered to every business type; otherwise only to that type (e.g. Agent plan).</summary>
    public BusinessType? ForBusinessType { get; set; }

    public bool IsActive { get; set; } = true;

    // ---- Tariff bands (like M-PESA tariffs): a plan can be one band of a measure ----
    /// <summary>Null = a classic plan (e.g. the trial plan). RentRoll / SaleAccounts = a tariff band of that measure.</summary>
    public TariffMetric? Metric { get; set; }
    /// <summary>The band covers BandFrom..BandTo inclusive (rent roll in KES, or instalment accounts). BandTo null = no upper limit.</summary>
    public decimal? BandFrom { get; set; }
    public decimal? BandTo { get; set; }
    /// <summary>Above the published bands: priced by agreement ("contact us").</summary>
    public bool IsQuoted { get; set; }
    public int SortOrder { get; set; }
}
