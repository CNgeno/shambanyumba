using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RealEstateERP.Core.Constants;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Security;

/// <summary>
/// Identity refreshes the cookie after a password, email or profile change. The default refresh would drop the selected
/// business; this keeps it. (BusinessContextSignIn.ClearBusinessAsync is the only place that drops it on purpose.)
/// </summary>
public sealed class AppSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    public static readonly string[] KeptClaimTypes = [.. AppClaimTypes.BusinessContext, "amr"];

    public override async Task RefreshSignInAsync(ApplicationUser user)
    {
        var current = await Context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var keep = current.Principal?.Claims
            .Where(c => KeptClaimTypes.Contains(c.Type))
            .Select(c => new Claim(c.Type, c.Value))
            .ToList() ?? [];

        if (current.Properties is null || !keep.Any(c => c.Type == AppClaimTypes.BusinessId))
        {
            await base.RefreshSignInAsync(user);
            return;
        }
        await SignInWithClaimsAsync(user, current.Properties, keep);
    }
}
