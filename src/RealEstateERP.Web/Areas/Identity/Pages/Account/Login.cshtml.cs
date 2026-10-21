using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; set; } = "/";
    public bool ShowResendLink { get; set; }
    [TempData] public string? StatusMessage { get; set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your email address."), EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Keep me signed in on this device")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = SafeReturnUrl(returnUrl);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = SafeReturnUrl(returnUrl);
        if (!ModelState.IsValid) return Page();

        var result = await signIn.PasswordSignInAsync(Input.Email.Trim(), Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (await users.FindByEmailAsync(Input.Email.Trim()) is { } user)
            {
                user.LastLoginAtUtc = DateTime.UtcNow;
                await users.UpdateAsync(user);
            }
            return LocalRedirect(ReturnUrl);
        }
        if (result.RequiresTwoFactor) return RedirectToPage("./LoginWith2fa", new { returnUrl = ReturnUrl, rememberMe = Input.RememberMe });
        if (result.IsLockedOut)
        {
            logger.LogWarning("Account locked out: {Email}", Input.Email);
            return RedirectToPage("./Lockout");
        }
        if (result.IsNotAllowed)
        {
            ShowResendLink = true;
            ModelState.AddModelError(string.Empty, "Please confirm your email address first. Check your inbox for the link we sent.");
            return Page();
        }

        ModelState.AddModelError(string.Empty, "That email and password don't match. After 5 wrong attempts the account is locked for 5 minutes.");
        return Page();
    }

    private string SafeReturnUrl(string? returnUrl) => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/");
}
