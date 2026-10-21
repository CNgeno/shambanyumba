using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record PartnerInput(string Name, PartnerKind Kind, string? Code, string? Phone, string? Email, decimal CommissionPerCustomer, bool IsActive, string? Notes);

public sealed record PartnerRow(int Id, string Name, string Code, PartnerKind Kind, decimal CommissionPerCustomer, bool IsActive,
    int Trials, int Paying, decimal Owed, decimal Paid, string? ReferrerBusinessName);

public sealed record ReferredBusiness(int BusinessId, string Name, DateTime CreatedAtUtc, bool Paying, int? CommissionId, decimal? Commission, CommissionStatus? Status, DateTime? PaidAtUtc, string? PaidReference);

public sealed record PartnerDetails(PartnerRow Partner, string? Phone, string? Email, string? Notes, IReadOnlyList<ReferredBusiness> Businesses);

/// <summary>A customer's own referral link: how many landlords they've brought, and free months earned.</summary>
public sealed record CustomerReferral(string Code, int Referred, int Paying);
