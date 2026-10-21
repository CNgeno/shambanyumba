using System.Security.Claims;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Services;

/// <summary>Reads identity + selected business from the auth cookie claims. No database access.</summary>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    // Background jobs (BusinessScope) act as the business with the Owner role and no user.
    private static BusinessScope.Context? Background => BusinessScope.Current;

    public bool IsAuthenticated => Background is not null || User?.Identity?.IsAuthenticated == true;

    public string? UserId => Background is not null ? null
        : User?.Identity?.IsAuthenticated == true ? User!.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public int? BusinessId => Background?.BusinessId
        ?? (int.TryParse(User?.FindFirstValue(AppClaimTypes.BusinessId), out var id) ? id : null);

    public BusinessRole? Role => Background is not null ? BusinessRole.Owner
        : Enum.TryParse<BusinessRole>(User?.FindFirstValue(AppClaimTypes.BusinessRole), out var role) ? role : null;

    public BusinessType? BusinessType => Background is not null ? Background.Type
        : Enum.TryParse<BusinessType>(User?.FindFirstValue(AppClaimTypes.BusinessType), out var type) ? type : null;

    public bool IsSuperAdmin => User?.IsInRole(AppRoles.SuperAdmin) == true;
}
