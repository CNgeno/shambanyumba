using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-10-B. The agency manages an owner's properties for a commission.
/// A property can be under only ONE active contract (ContractProperty + filtered unique index).
/// </summary>
public class ManagementContract : TenantEntity
{
    public string ContractNumber { get; set; } = string.Empty;

    public int OwnerId { get; set; }
    public PropertyOwner Owner { get; set; } = null!;

    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public CommissionType CommissionType { get; set; } = CommissionType.PercentOfCollections;
    public decimal CommissionPercent { get; set; }
    public decimal FixedMonthlyFee { get; set; }

    /// <summary>Day of the month by which the owner is paid for the previous month (1-28).</summary>
    public int RemittanceDay { get; set; } = 10;

    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public DateOnly? EndedOn { get; set; }
    public string? Notes { get; set; }

    public ICollection<ContractProperty> Properties { get; set; } = new List<ContractProperty>();
}

/// <summary>A property covered by a contract, with the dates it was covered (so past months stay correct).</summary>
public class ContractProperty : TenantEntity
{
    public int ContractId { get; set; }
    public ManagementContract Contract { get; set; } = null!;

    public int AssetId { get; set; }
    public Asset Asset { get; set; } = null!;

    public DateOnly AddedOn { get; set; }
    public DateOnly? RemovedOn { get; set; }

    /// <summary>True while the contract is active and the property hasn't been removed.</summary>
    public bool IsActive { get; set; }
}
