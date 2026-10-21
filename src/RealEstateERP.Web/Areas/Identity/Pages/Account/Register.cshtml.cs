using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Services;
using RealEstateERP.Web.Validation;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<ApplicationUser> users, AccountEmails emails, ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; set; } = "/";

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your name."), StringLength(150)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Enter your email address."), EmailAddress]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Choose a password."), StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
        public string ConfirmPassword { get; set; } = "";

        [MustBeTrue(ErrorMessage = "Accept the terms to continue.")]
        public bool AcceptTerms { get; set; }
    }

    public void OnGet(string? returnUrl = null, string? email = null)
    {
        ReturnUrl = Safe(returnUrl);
        if (!string.IsNullOrWhiteSpace(email)) Input.Email = email;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = Safe(returnUrl);
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim();
        var user = new ApplicationUser { UserName = email, Email = email, FullName = Input.FullName.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Code is "DuplicateUserName" or "DuplicateEmail"
                    ? "An account with this email already exists. Sign in, or reset your password."
                    : error.Description);
            return Page();
        }

        logger.LogInformation("New account created: {Email}", email);
        await emails.SendConfirmationAsync(user, ReturnUrl);
        return RedirectToPage("./RegisterConfirmation", new { email, returnUrl = ReturnUrl });
    }

    private string Safe(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : Url.Content("~/");
}
