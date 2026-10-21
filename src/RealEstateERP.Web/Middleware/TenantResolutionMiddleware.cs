using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Middleware;

/// <summary>
/// FR-01-C: a signed-in user must have a business selected before reaching any business data.
/// Also re-checks (cached for 2 minutes) that the membership still exists and the role is unchanged,
/// so removing someone from a business takes effect quickly.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private const string SelectPath = "/Business/Select";
    private static readonly TimeSpan MembershipCacheDuration = TimeSpan.FromMinutes(2);

    public static string MembershipCacheKey(string userId, int businessId) => $"membership:{userId}:{businessId}";

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUserService currentUser,
        IBusinessService businesses,
        IMemoryCache cache,
        UserManager<ApplicationUser> users,
        BusinessContextSignIn businessSignIn)
    {
        var endpoint = context.GetEndpoint();

        if (endpoint is null
            || !currentUser.IsAuthenticated
            || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint.Metadata.GetMetadata<AllowWithoutBusinessAttribute>() is not null
            || IsIdentityArea(context))
        {
            await next(context);
            return;
        }

        if (currentUser.BusinessId is not int businessId)
        {
            Redirect(context, SelectPath);
            return;
        }

        var userId = currentUser.UserId!;
        var membership = await cache.GetOrCreateAsync<UserBusinessSummary?>(
            MembershipCacheKey(userId, businessId),
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = MembershipCacheDuration;
                return await businesses.GetMembershipAsync(userId, businessId, context.RequestAborted);
            });

        if (membership is null || membership.Role != currentUser.Role)
        {
            var user = await users.GetUserAsync(context.User);
            if (user is not null)
                await businessSignIn.ClearBusinessAsync(user);

            Redirect(context, SelectPath);
            return;
        }

        await next(context);
    }

    private static bool IsIdentityArea(HttpContext context)
        => string.Equals(context.Request.RouteValues["area"] as string, "Identity", StringComparison.OrdinalIgnoreCase);

    private static void Redirect(HttpContext context, string path)
    {
        // AJAX callers get a status + header instead of an HTML redirect they can't follow.
        if (context.Request.Headers.XRequestedWith == "XMLHttpRequest")
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers["X-Redirect-To"] = path;
            return;
        }
        context.Response.Redirect(path);
    }
}
