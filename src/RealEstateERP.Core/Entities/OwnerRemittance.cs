using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-10-C. Monthly statement to an owner: collections per property, less commission, plus/minus adjustments.
/// Built from payments actually received in the month (cash basis); deposits are excluded (held for tenants).
/// Totals are a snapshot taken when the draft is created; later payment reversals are corrected by adjustments.
/// </summary>
public class OwnerRemittance : TenantEntity
{
    public string RemittanceNumber { get; set; } = string.Empty;

    public int ContractId { get; set; }
    public ManagementContract Contract { get; set; } = null!;

    public int OwnerId { get; set; }
    public PropertyOwner Owner { get; set; } = null!;

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal GrossCollections { get; set; }
    public decimal DepositsExcluded { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal AdjustmentsTotal { get; set; }
    public decimal NetPayable { get; set; }

    public RemittanceStatus Status { get; set; } = RemittanceStatus.Draft;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateOnly? PaidOn { get; set; }
    public PayoutMethod? PaidMethod { get; set; }
    public string? PaidReference { get; set; }
    public string? StatusReason { get; set; }
    public string? Notes { get; set; }

    public ICollection<RemittanceLine> Lines { get; set; } = new List<RemittanceLine>();
}

public class RemittanceLine : TenantEntity
{
    public int RemittanceId { get; set; }
    public OwnerRemittance Remittance { get; set; } = null!;

    public RemittanceLineType LineType { get; set; }
    public int? AssetId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Signed: collections positive, commission negative, adjustments either way.</summary>
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }
}
