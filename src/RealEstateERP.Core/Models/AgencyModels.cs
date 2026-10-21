using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record OwnerInput(
    OwnerType Type, string FullName, string? IdNumber, string? KraPin, string? Phone, string? Email, string? PostalAddress,
    PayoutMethod PayoutMethod, string? BankName, string? BankAccountName, string? BankAccountNumber, string? MpesaNumber, string? Notes);

public sealed record AgentInput(string FullName, string? LicenseNumber, string? Phone, string? Email, decimal CommissionSharePercent, bool IsActive, string? UserId = null);

/// <summary>Who the signed-in person is in agency terms. Restricted = sees only their own contracts.</summary>
public sealed record AgentScope(int? AgentId, string? AgentName, bool IsRestricted)
{
    public static AgentScope None { get; } = new(null, null, false);
}

public sealed record LinkableMember(string UserId, string Name, string Email, string? LinkedAgentName);

public sealed record ContractInput(
    int OwnerId, int? AgentId, DateOnly StartDate, DateOnly? EndDate, CommissionType CommissionType,
    decimal CommissionPercent, decimal FixedMonthlyFee, int RemittanceDay, string? Notes);

public sealed record OwnerSummary(int Id, string FullName, OwnerType Type, string? Phone, string? Email, int ActiveContracts, int Properties);

public sealed record AgentSummary(int Id, string FullName, string? LicenseNumber, string? Phone, decimal CommissionSharePercent, bool IsActive, int ActiveContracts, string? LoginEmail = null);

public sealed record ContractSummary(
    int Id, string ContractNumber, int OwnerId, string OwnerName, int? AgentId, string? AgentName,
    DateOnly StartDate, DateOnly? EndDate, CommissionType CommissionType, decimal CommissionPercent, decimal FixedMonthlyFee,
    ContractStatus Status, int PropertyCount, string? PropertyNames);

public sealed record ContractPropertyRow(int LinkId, int AssetId, string AssetName, int Units, DateOnly AddedOn, DateOnly? RemovedOn, bool IsActive);

public sealed record ContractDetails(ManagementContract Contract, IReadOnlyList<ContractPropertyRow> Properties, IReadOnlyList<RemittanceSummary> Remittances);

public sealed record AvailableProperty(int AssetId, string Name, int Units);

public sealed record RemittancePropertyRow(int AssetId, string AssetName, decimal Collected, decimal DepositsExcluded, decimal Income);

public sealed record RemittancePreview(
    ManagementContract Contract, DateOnly PeriodStart, DateOnly PeriodEnd, IReadOnlyList<RemittancePropertyRow> Properties,
    decimal Income, decimal DepositsExcluded, decimal Commission, decimal NetPayable, bool AlreadyExists);

public sealed record RemittanceSummary(
    int Id, string RemittanceNumber, int ContractId, string ContractNumber, int OwnerId, string OwnerName,
    DateOnly PeriodStart, decimal GrossCollections, decimal CommissionAmount, decimal NetPayable, RemittanceStatus Status, DateOnly? DueBy);

public sealed record RemittanceDetails(OwnerRemittance Remittance, IReadOnlyList<RemittanceLine> Lines, DateOnly DueBy);

public sealed record AgentCommissionRow(int? AgentId, string AgentName, decimal SharePercent, int Remittances, decimal AgencyCommission, decimal AgentShare);
