using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

/// <summary>
/// The link in the sign-up email lands here. A good link confirms the address and goes straight to sign in,
/// with the email filled in and the original returnUrl kept; a bad or expired link shows how to get a new one.
/// </summary>
[AllowAnonymous]
public class ConfirmEmailModel(UserManager<ApplicationUser> users, ILogger<ConfirmEmailModel> logger) : PageModel
{
    [TempData] public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? code, string? returnUrl = null)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code)) return Page();

        var user = await users.FindByIdAsync(userId);
        if (user is null) return Page();

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)); }
        catch (FormatException) { return Page(); }

        var wasConfirmed = user.EmailConfirmed;
        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            logger.LogWarning("Email confirmation failed for {UserId}: {Errors}", userId,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return Page();
        }
        StatusMessage = wasConfirmed
            ? "Your email was already confirmed. Sign in to continue."
            : "Your email is confirmed. Sign in to start using your account.";

        // Login checks returnUrl again (local or trusted host only), so nothing unsafe is passed through.
        return RedirectToPage("./Login", new { returnUrl, email = user.Email });
    }
}
