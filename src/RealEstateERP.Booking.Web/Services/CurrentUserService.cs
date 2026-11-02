using System.Security.Claims;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Booking.Web.Services;

/// <summary>Who is signed in, and in which business, read from the shared ShambaNyumba sign-in cookie.</summary>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public string? UserId => IsAuthenticated ? User!.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    public int? BusinessId => int.TryParse(User?.FindFirstValue(AppClaimTypes.BusinessId), out var id) ? id : null;
    public BusinessRole? Role => Enum.TryParse<BusinessRole>(User?.FindFirstValue(AppClaimTypes.BusinessRole), out var r) ? r : null;
    public BusinessType? BusinessType => Enum.TryParse<BusinessType>(User?.FindFirstValue(AppClaimTypes.BusinessType), out var t) ? t : null;
    public bool IsSuperAdmin => User?.IsInRole(AppRoles.SuperAdmin) == true;
}
