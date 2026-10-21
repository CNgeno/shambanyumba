using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class BuyerForm
{
    public int? Id { get; set; }
    public OwnerType Type { get; set; } = OwnerType.Individual;
    [Required(ErrorMessage = "Enter the buyer's name."), StringLength(150), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [StringLength(50), Display(Name = "ID / registration number")] public string? IdNumber { get; set; }
    [StringLength(20), Display(Name = "KRA PIN")] public string? KraPin { get; set; }
    [StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(300), Display(Name = "Postal address")] public string? PostalAddress { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    /// <summary>When adding a buyer from the new-sale form, go back there afterwards.</summary>
    public bool ReturnToSale { get; set; }

    public BuyerInput ToInput() => new(Type, FullName, IdNumber, KraPin, Phone, Email, PostalAddress, Notes);
}

public sealed class BuyerDetailsViewModel
{
    public required Buyer Buyer { get; init; }
    public required IReadOnlyList<SaleSummary> Sales { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class SalesIndexViewModel
{
    public required IReadOnlyList<SaleSummary> Sales { get; init; }
    public required SalesSummaryTotals Totals { get; init; }
    public required SalesListView View { get; init; }
    public string? Search { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class SaleForm
{
    public IReadOnlyList<BuyerSummary> Buyers { get; set; } = [];
    public IReadOnlyList<SellableUnit> Units { get; set; } = [];
    public IReadOnlyList<AgentSummary> Agents { get; set; } = [];
    public bool ShowAgent { get; set; }

    [Required(ErrorMessage = "Choose the buyer."), Display(Name = "Buyer")] public int? BuyerId { get; set; }
    [Required(ErrorMessage = "Choose the unit or plot."), Display(Name = "Unit / plot")] public int? UnitId { get; set; }
    [Display(Name = "Agent")] public int? AgentId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Sale date")] public DateTime? SaleDate { get; set; }
    [Range(1, 10_000_000_000), Display(Name = "Sale price (KES)")] public decimal Price { get; set; }
    [Display(Name = "Payment plan")] public PaymentPlan PaymentPlan { get; set; } = PaymentPlan.Instalments;
    [Range(0, 10_000_000_000), Display(Name = "Deposit (KES)")] public decimal DepositAmount { get; set; }
    [Range(1, 120), Display(Name = "Number of instalments")] public int InstalmentCount { get; set; } = 12;
    [Display(Name = "Every")] public InstalmentFrequency InstalmentFrequency { get; set; } = InstalmentFrequency.Monthly;
    [DataType(DataType.Date), Display(Name = "First instalment due")] public DateTime? FirstInstalmentDate { get; set; }
    [Range(0, 20), Display(Name = "Commission (%)")] public decimal CommissionPercent { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }

    public SaleInput ToInput() => new(BuyerId!.Value, UnitId!.Value, AgentId, DateOnly.FromDateTime(SaleDate!.Value), Price, PaymentPlan, DepositAmount,
        InstalmentCount, InstalmentFrequency, FirstInstalmentDate is DateTime f ? DateOnly.FromDateTime(f) : null, CommissionPercent, Notes);
}

public sealed class SaleDetailsViewModel
{
    public required SaleDetails Details { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAdmin { get; init; }
    public required DateOnly Today { get; init; }
}

public sealed class SalesReportsViewModel
{
    public required SalesSummaryTotals Totals { get; init; }
    public required IReadOnlyList<InventoryRow> Inventory { get; init; }
    public required IReadOnlyList<ArrearsSaleRow> Arrears { get; init; }
    public required IReadOnlyList<SaleSummary> Pipeline { get; init; }
    public required IReadOnlyList<SaleSummary> Sold { get; init; }
}
