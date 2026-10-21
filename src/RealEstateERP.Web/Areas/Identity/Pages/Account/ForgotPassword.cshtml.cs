using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ForgotPasswordModel(UserManager<ApplicationUser> users, AccountEmails emails) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter your email address."), EmailAddress]
    public string Email { get; set; } = "";

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Same response whether or not the account exists, so this page can't be used to discover accounts.
        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is not null && await users.IsEmailConfirmedAsync(user))
            await emails.SendPasswordResetAsync(user);

        return RedirectToPage("./ForgotPasswordConfirmation");
    }
}
