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
    public int SortOrder { get; set; }
}
