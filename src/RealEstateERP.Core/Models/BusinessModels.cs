using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record UserBusinessSummary(int BusinessId, string BusinessName, BusinessType Type, BusinessRole Role);

public sealed record CreateBusinessRequest(
    string Name,
    BusinessType Type,
    string? Email,
    string? Phone,
    string? PhysicalAddress,
    string? KraPin);
