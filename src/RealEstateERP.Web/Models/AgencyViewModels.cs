using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

public sealed class OwnerForm
{
    public int? Id { get; set; }
    public OwnerType Type { get; set; } = OwnerType.Individual;
    [Required(ErrorMessage = "Enter the owner's name."), StringLength(150), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [StringLength(50), Display(Name = "ID / registration number")] public string? IdNumber { get; set; }
    [StringLength(20), Display(Name = "KRA PIN")] public string? KraPin { get; set; }
    [StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [StringLength(300), Display(Name = "Postal address")] public string? PostalAddress { get; set; }
    [Display(Name = "Pay the owner by")] public PayoutMethod PayoutMethod { get; set; } = PayoutMethod.Bank;
    [StringLength(100), Display(Name = "Bank")] public string? BankName { get; set; }
    [StringLength(150), Display(Name = "Account name")] public string? BankAccountName { get; set; }
    [StringLength(50), Display(Name = "Account number")] public string? BankAccountNumber { get; set; }
    [StringLength(20), Display(Name = "M-PESA number")] public string? MpesaNumber { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }

    public OwnerInput ToInput() => new(Type, FullName, IdNumber, KraPin, Phone, Email, PostalAddress, PayoutMethod, BankName, BankAccountName, BankAccountNumber, MpesaNumber, Notes);
}

public sealed class OwnersIndexViewModel
{
    public required IReadOnlyList<OwnerSummary> Owners { get; init; }
    public bool IsAgent { get; init; }
    public string? Search { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class OwnerDetailsViewModel
{
    public required PropertyOwner Owner { get; init; }
    public required IReadOnlyList<ContractSummary> Contracts { get; init; }
    public required IReadOnlyList<RemittanceSummary> Remittances { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class AgentForm
{
    public int? Id { get; set; }
    [Required(ErrorMessage = "Enter the agent's name."), StringLength(150), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [StringLength(50), Display(Name = "EARB licence number")] public string? LicenseNumber { get; set; }
    [StringLength(20)] public string? Phone { get; set; }
    [EmailAddress, StringLength(256)] public string? Email { get; set; }
    [Range(0, 100), Display(Name = "Agent's share of commission (%)")] public decimal CommissionSharePercent { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
    [Display(Name = "Login (team member)")] public string? UserId { get; set; }
    public IReadOnlyList<LinkableMember> Members { get; set; } = [];

    public AgentInput ToInput() => new(FullName, LicenseNumber, Phone, Email, CommissionSharePercent, IsActive, string.IsNullOrWhiteSpace(UserId) ? null : UserId);
}

public sealed class ContractForm
{
    public int? Id { get; set; }
    public string? ContractNumber { get; set; }
    public ContractStatus Status { get; set; }
    public IReadOnlyList<OwnerSummary> Owners { get; set; } = [];
    public IReadOnlyList<AgentSummary> Agents { get; set; } = [];

    [Required(ErrorMessage = "Choose the owner."), Display(Name = "Owner")] public int? OwnerId { get; set; }
    [Display(Name = "Agent")] public int? AgentId { get; set; }
    [Required, DataType(DataType.Date), Display(Name = "Start date")] public DateTime? StartDate { get; set; }
    [DataType(DataType.Date), Display(Name = "End date")] public DateTime? EndDate { get; set; }
    [Display(Name = "Commission")] public CommissionType CommissionType { get; set; } = CommissionType.PercentOfCollections;
    [Range(0, 50), Display(Name = "Commission (%)")] public decimal CommissionPercent { get; set; } = 10;
    [Range(0, 10_000_000), Display(Name = "Fixed monthly fee (KES)")] public decimal FixedMonthlyFee { get; set; }
    [Range(1, 28), Display(Name = "Pay the owner by day")] public int RemittanceDay { get; set; } = 10;
    [StringLength(1000)] public string? Notes { get; set; }

    public ContractInput ToInput() => new(OwnerId!.Value, AgentId, DateOnly.FromDateTime(StartDate!.Value),
        EndDate is DateTime e ? DateOnly.FromDateTime(e) : null, CommissionType, CommissionPercent, FixedMonthlyFee, RemittanceDay, Notes);
}

public sealed class ContractsIndexViewModel
{
    public required IReadOnlyList<ContractSummary> Contracts { get; init; }
    public ContractStatus? Status { get; init; }
    public required bool CanEdit { get; init; }
    public bool IsAgent { get; init; }
}

public sealed class ContractDetailsViewModel
{
    public required ContractDetails Details { get; init; }
    public required IReadOnlyList<AvailableProperty> Available { get; init; }
    /// <summary>Change terms, properties, activate, end: Principal/Admin/User, never a restricted agent.</summary>
    public required bool CanEdit { get; init; }
    /// <summary>Prepare statements: anyone who can edit, including the contract's own agent.</summary>
    public bool CanPrepare { get; init; }
    public required DateOnly Today { get; init; }
}

public sealed class RemittancesIndexViewModel
{
    public required IReadOnlyList<RemittanceSummary> Remittances { get; init; }
    public required IReadOnlyList<ContractSummary> ActiveContracts { get; init; }
    public required string Month { get; init; }
    public RemittanceStatus? Status { get; init; }
    public required bool CanEdit { get; init; }
}

public sealed class RemittanceDetailsViewModel
{
    public required RemittanceDetails Details { get; init; }
    public required bool CanEdit { get; init; }
    public required bool CanAdmin { get; init; }
}

public sealed class AgentCommissionsViewModel
{
    public required IReadOnlyList<AgentCommissionRow> Rows { get; init; }
    public IReadOnlyList<SalesCommissionRow> SalesRows { get; init; } = [];
    public required string Month { get; init; }
}
