using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Interfaces;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    string? UserId { get; }
    int? BusinessId { get; }
    BusinessRole? Role { get; }
    BusinessType? BusinessType { get; }
    bool IsSuperAdmin { get; }
}
