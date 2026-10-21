using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>The tenant itself. Not an ITenantEntity.</summary>
public class Business : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public BusinessType Type { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhysicalAddress { get; set; }
    public string? KraPin { get; set; }
    public string? LogoPath { get; set; }
    public string TimeZoneId { get; set; } = "Africa/Nairobi";

    /// <summary>FR-15. Changed ONLY together with a CreditTransaction (see CreditLedger), never on its own.</summary>
    public int CreditBalance { get; set; }

    /// <summary>Public demo business: anyone can sign in to it, it's reset every night, it never emails and never
    /// appears on the public marketplace.</summary>
    public bool IsDemo { get; set; }

    /// <summary>The partner (influencer, business developer or customer) who brought this business, from a link or code.</summary>
    public int? ReferredByPartnerId { get; set; }

    /// <summary>
    /// Rental menus (tenants, leases, invoices, payments, arrears, reports). Null = default for the business type:
    /// off for developers, on for everyone else. Developers who also let units switch it on in Business settings.
    /// </summary>
    public bool? ShowRentalTools { get; set; }

    // ---- Automation (Business settings > Automation) ----
    /// <summary>Create and issue this month's invoices automatically on AutoInvoiceDay (1-28).</summary>
    public bool AutoInvoiceEnabled { get; set; }
    public int AutoInvoiceDay { get; set; } = 1;
    /// <summary>Apply late penalties automatically every day (same rules as the manual preview).</summary>
    public bool AutoPenaltiesEnabled { get; set; }
    /// <summary>Business-level overrides of the system penalty defaults (a unit's own settings still win).</summary>
    public int? PenaltyGraceDays { get; set; }
    public decimal? PenaltyPercent { get; set; }
    public bool EmailInvoicesToTenants { get; set; }
    public bool EmailReceiptsToTenants { get; set; }
    /// <summary>The daily job lists every vacant rental unit that has no listing yet (shown only while subscribed).</summary>
    public bool AutoListVacantUnits { get; set; }
    /// <summary>How tenants pay, printed on invoice emails (paybill, account format, bank).</summary>
    public string? PaymentInstructions { get; set; }
    /// <summary>Set when the daily job claims this business for a day (rowversion stops two servers doing it twice).</summary>
    public DateOnly? LastAutomationRunOn { get; set; }
    public DateOnly? LastAutoInvoiceMonth { get; set; }
    public string? LastAutomationSummary { get; set; }

    public ICollection<BusinessUser> Members { get; set; } = new List<BusinessUser>();

    // Sprint 1 adds Subscriptions as a ONE-WAY link (Subscription -> Business).
    // Do not add a CurrentSubscriptionId here: a two-way FK is one of the cycles we are avoiding.
}
