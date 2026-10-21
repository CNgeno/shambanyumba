using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record TenantInput(
    TenantType Type,
    string FullName,
    IdDocumentType? IdType,
    string? IdNumber,
    string? KraPin,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? PostalAddress,
    string? Employer,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    string? EmergencyContactRelationship,
    string? Notes);

public sealed record TenantListFilter(TenantStatus? Status, int? AssetId, string? Search);

public sealed record TenantSummary(
    int Id, string FullName, TenantType Type, string? Phone, string? IdNumber,
    TenantStatus Status, string? CurrentUnits, DateOnly? NextLeaseEnd);

public sealed record TenantOption(int Id, string FullName, string? Phone);

public sealed record TenantDocumentSummary(
    int Id, int TenantId, int? LeaseId, TenantDocumentType DocumentType, string? Title,
    string FileName, long SizeBytes, DateTime UploadedAtUtc);

public sealed record TenantDetails(
    Tenant Tenant,
    TenantStatus Status,
    IReadOnlyList<LeaseSummary> Leases,
    IReadOnlyList<TenantDocumentSummary> Documents);

public sealed record LeaseInput(
    int TenantId,
    int UnitId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    int NoticePeriodDays,
    int RentDueDay,
    string? Notes);

public sealed record LeaseRenewalInput(
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal MonthlyRent,
    decimal DepositAmount,
    int NoticePeriodDays,
    int RentDueDay,
    string? Notes);

public sealed record LeaseTerminationInput(
    DateOnly EndDate,
    DateOnly? NoticeGivenOn,
    string Reason,
    decimal Penalty,
    decimal DepositDeductions,
    string? DepositNote,
    bool MakeUnitInactive);

public sealed record LeaseListFilter(LeaseListView View, int? AssetId, string? Search);

public sealed record LeaseSummary(
    int Id, string LeaseNumber, int TenantId, string TenantName, int UnitId, string UnitNumber,
    int AssetId, string AssetName, DateOnly StartDate, DateOnly? EndDate, decimal MonthlyRent,
    LeaseStatus Status, LeaseExpiry Expiry);

public sealed record LeaseDetails(
    Lease Lease,
    LeaseExpiry Expiry,
    LeaseSummary? RenewedFrom,
    LeaseSummary? RenewedTo,
    IReadOnlyList<TenantDocumentSummary> Documents);

/// <summary>A unit that can take a new lease (Available or Reserved).</summary>
public sealed record LettableUnit(int Id, string UnitNumber, int AssetId, string AssetName, string? NodePath, decimal MonthlyRent, UnitStatus Status);

public sealed record LeaseCounts(int Active, int EndingWithin90, int Ended, int Drafts);
