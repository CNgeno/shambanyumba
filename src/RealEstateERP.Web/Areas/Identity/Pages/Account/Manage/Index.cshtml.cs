using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RealEstateERP.Core.Common;
using RealEstateERP.Infrastructure.Identity;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account.Manage;

public class IndexModel(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    [TempData] public string? StatusMessage { get; set; }
    public string Email { get; private set; } = "";
    public DateTime MemberSince { get; private set; }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "Enter your name."), StringLength(150)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [StringLength(20)]
        [Display(Name = "Phone number", Prompt = "0712 345 678")]
        public string? PhoneNumber { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        Load(user);
        Input = new InputModel { FullName = user.FullName ?? "", PhoneNumber = KenyanPhone.Display(user.PhoneNumber) };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        Load(user);
        if (!ModelState.IsValid) return Page();

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(Input.PhoneNumber) && (phone = KenyanPhone.Normalize(Input.PhoneNumber)) is null)
        {
            ModelState.AddModelError("Input.PhoneNumber", "Enter a valid phone number, e.g. 0712 345 678.");
            return Page();
        }

        user.FullName = Input.FullName.Trim();
        user.PhoneNumber = phone;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return Page();
        }

        await signIn.RefreshSignInAsync(user);   // updates the name in the menu; keeps the selected business
        StatusMessage = "Profile saved.";
        return RedirectToPage();
    }

    private void Load(ApplicationUser user)
    {
        ViewData["ActivePage"] = "Index";
        Email = user.Email ?? "";
        MemberSince = user.CreatedAtUtc;
    }
}
