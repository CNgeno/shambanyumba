using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Services;

/// <summary>Confirmation and password-reset emails, with absolute links built from the current request.</summary>
public sealed class AccountEmails(
    UserManager<ApplicationUser> users,
    IEmailService email,
    ISystemConfigService config,
    LinkGenerator links,
    IHttpContextAccessor http)
{
    public async Task SendConfirmationAsync(ApplicationUser user, string? returnUrl = null)
    {
        var code = Encode(await users.GenerateEmailConfirmationTokenAsync(user));
        var url = links.GetUriByPage(http.HttpContext!, "/Account/ConfirmEmail", null,
            new { area = "Identity", userId = user.Id, code, returnUrl })!;
        var platform = await PlatformAsync();
        await email.SendAsync(new EmailMessage(user.Email!, $"Confirm your {platform} account",
            EmailTemplates.Render(platform, "Confirm your email address",
                [$"Hi {FirstName(user)}, thanks for signing up.", "Confirm your email address to start using your account."],
                "Confirm email", url, "If you didn't create an account, you can ignore this email.")));
    }

    public async Task SendPasswordResetAsync(ApplicationUser user)
    {
        var code = Encode(await users.GeneratePasswordResetTokenAsync(user));
        var url = links.GetUriByPage(http.HttpContext!, "/Account/ResetPassword", null, new { area = "Identity", code })!;
        var platform = await PlatformAsync();
        await email.SendAsync(new EmailMessage(user.Email!, $"Reset your {platform} password",
            EmailTemplates.Render(platform, "Reset your password",
                [$"Hi {FirstName(user)}, we received a request to reset your password.", "The link works once and expires after a short time."],
                "Choose a new password", url, "If you didn't ask for this, ignore this email: your password stays the same.")));
    }

    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string FirstName(ApplicationUser u) => (u.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "there";
    private async Task<string> PlatformAsync() => await config.GetAsync(ConfigKeys.PlatformName) ?? "ShambaNyumba";
}
