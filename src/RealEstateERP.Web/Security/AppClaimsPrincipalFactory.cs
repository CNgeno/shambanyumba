using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RealEstateERP.Core.Constants;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Security;

/// <summary>Adds the user's full name to the cookie so layouts can greet them without a DB query.</summary>
public sealed class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.FullName))
            identity.AddClaim(new Claim(AppClaimTypes.FullName, user.FullName));
        return identity;
    }
}
