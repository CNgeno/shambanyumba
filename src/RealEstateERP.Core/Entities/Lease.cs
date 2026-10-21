using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-05. Links a tenant to a unit on agreed terms.
/// The database allows only ONE Active lease per unit (filtered unique index).
/// Renewal creates a new lease pointing back at the old one (one-way link: new -> old).
/// </summary>
public class Lease : TenantEntity
{
    public string LeaseNumber { get; set; } = string.Empty;

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public LeaseStatus Status { get; set; } = LeaseStatus.Draft;

    public DateOnly StartDate { get; set; }
    /// <summary>Null = open-ended (month to month).</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Agreed rent, a snapshot. Changing the unit's rent later doesn't change this lease.</summary>
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public int NoticePeriodDays { get; set; } = 30;

    /// <summary>Day of the month rent falls due (1-28). Used by invoicing in Sprint 4.</summary>
    public int RentDueDay { get; set; } = 1;

    public string? Notes { get; set; }

    public DateTime? ActivatedAtUtc { get; set; }

    // ---- Ending ----
    /// <summary>The day the lease actually ended (termination date, expiry date, or day before a renewal).</summary>
    public DateOnly? EndedOn { get; set; }
    public DateOnly? NoticeGivenOn { get; set; }
    public string? TerminationReason { get; set; }
    public decimal? TerminationPenalty { get; set; }
    public decimal? DepositDeductions { get; set; }
    public string? DepositNote { get; set; }

    // ---- Renewal (one-way: the NEW lease points at the old one) ----
    public int? RenewedFromLeaseId { get; set; }
    public Lease? RenewedFrom { get; set; }

    /// <summary>Deposit due back to the tenant once the lease has ended.</summary>
    public decimal? DepositRefundDue => Status is LeaseStatus.Terminated or LeaseStatus.Expired
        ? Math.Max(0, DepositAmount - (DepositDeductions ?? 0))
        : null;
}
