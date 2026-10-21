using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-11-A. A unit sold to a buyer, paid in full or by instalments. The database allows one live sale per unit.
/// SaleService is the only code that moves a unit to Reserved/Sold for a sale and the only writer of AmountPaid.
/// </summary>
public class Sale : TenantEntity
{
    public string SaleNumber { get; set; } = string.Empty;

    public int BuyerId { get; set; }
    public Buyer Buyer { get; set; } = null!;

    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    /// <summary>Agency sales: the agent earning a share of the commission.</summary>
    public int? AgentId { get; set; }
    public Agent? Agent { get; set; }

    public DateOnly SaleDate { get; set; }
    public decimal Price { get; set; }
    public decimal DepositAmount { get; set; }
    public PaymentPlan PaymentPlan { get; set; }
    public int InstalmentCount { get; set; }
    public InstalmentFrequency InstalmentFrequency { get; set; } = InstalmentFrequency.Monthly;
    public DateOnly? FirstInstalmentDate { get; set; }

    /// <summary>Commission on money collected (agencies selling for a developer, or a developer paying an agent).</summary>
    public decimal CommissionPercent { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Reserved;
    public decimal AmountPaid { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public DateOnly? CancelledOn { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }

    public ICollection<SaleInstalment> Instalments { get; set; } = new List<SaleInstalment>();
    public ICollection<SaleMilestone> Milestones { get; set; } = new List<SaleMilestone>();

    public decimal Balance => Price - AmountPaid;
}

/// <summary>One scheduled payment. Number 0 is the deposit.</summary>
public class SaleInstalment : TenantEntity
{
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public int Number { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
}

/// <summary>FR-11-B. A transfer step with its date, reference and an optional document.</summary>
public class SaleMilestone : TenantEntity
{
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;

    public MilestoneType Type { get; set; }
    public DateOnly? CompletedOn { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? DocumentStorageKey { get; set; }
    public string? DocumentFileName { get; set; }
}
