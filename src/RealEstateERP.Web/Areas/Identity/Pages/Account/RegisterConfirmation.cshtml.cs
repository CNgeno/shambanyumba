using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RealEstateERP.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterConfirmationModel(IWebHostEnvironment env) : PageModel
{
    public string? Email { get; private set; }
    public bool IsDevelopment => env.IsDevelopment();

    public void OnGet(string? email) => Email = email;
}
