using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ResetPasswordModel(UserManager<ApplicationUser> users) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your email address."), EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Choose a new password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password), Display(Name = "New password")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm new password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public string Code { get; set; } = "";
    }

    public IActionResult OnGet(string? code = null)
    {
        if (string.IsNullOrEmpty(code)) return BadRequest("A reset code is needed. Use the link from the email.");
        Input.Code = code;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        if (user is null) return RedirectToPage("./ResetPasswordConfirmation");   // don't reveal whether the account exists

        string token;
        try { token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Code)); }
        catch (FormatException) { ModelState.AddModelError(string.Empty, "This reset link is damaged. Ask for a new one."); return Page(); }

        var result = await users.ResetPasswordAsync(user, token, Input.Password);
        if (result.Succeeded)
        {
            await users.SetLockoutEndDateAsync(user, null);   // a successful reset also lifts a lockout
            await users.ResetAccessFailedCountAsync(user);
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Code == "InvalidToken"
                ? "This reset link has expired or was already used. Ask for a new one."
                : error.Description);
        return Page();
    }
}
