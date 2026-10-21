using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Services;

/// <summary>Puts the selected business into (or removes it from) the auth cookie.</summary>
public sealed class BusinessContextSignIn(SignInManager<ApplicationUser> signIn, IBusinessService businesses)
{
    public async Task SelectBusinessAsync(ApplicationUser user, UserBusinessSummary membership)
    {
        // Keep "remember me" and other cookie properties from the current sign-in.
        var current = await signIn.Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var properties = current.Properties ?? new AuthenticationProperties();

        Claim[] claims =
        [
            new(AppClaimTypes.BusinessId, membership.BusinessId.ToString(CultureInfo.InvariantCulture)),
            new(AppClaimTypes.BusinessRole, membership.Role.ToString()),
            new(AppClaimTypes.BusinessName, membership.BusinessName),
            new(AppClaimTypes.BusinessType, membership.Type.ToString()),
            new(AppClaimTypes.RentalTools, ((await businesses.GetProfileAsync(membership.BusinessId))?.RentalToolsEnabled
                ?? RealEstateERP.Core.Common.RentalTools.Enabled(membership.Type, null)) ? "true" : "false")
        ];

        await signIn.SignInWithClaimsAsync(user, properties, claims);
    }

    /// <summary>Re-issues the cookie WITHOUT business claims ("Switch business", or the membership was removed).</summary>
    public async Task ClearBusinessAsync(ApplicationUser user)
    {
        var current = await signIn.Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var amr = current.Principal?.FindAll("amr").Select(c => new Claim(c.Type, c.Value)).ToList() ?? [];
        await signIn.SignInWithClaimsAsync(user, current.Properties ?? new AuthenticationProperties(), amr);
    }
}
