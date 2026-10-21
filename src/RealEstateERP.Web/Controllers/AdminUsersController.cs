using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/users")]
public class AdminUsersController(
    IAdminDirectoryService directory,
    UserManager<ApplicationUser> users,
    AccountEmails emails) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, CancellationToken ct)
        => View(new AdminUsersViewModel { Users = await directory.GetUsersAsync(q, ct), Search = q, CurrentUserId = users.GetUserId(User)! });

    [HttpPost("{id}/confirm"), ValidateAntiForgeryToken]
    public Task<IActionResult> ConfirmEmail(string id) => ActAsync(id, async u =>
    {
        u.EmailConfirmed = true;
        return (await users.UpdateAsync(u), "Email marked as confirmed.");
    });

    [HttpPost("{id}/lock"), ValidateAntiForgeryToken]
    public Task<IActionResult> Lock(string id) => ActAsync(id, async u =>
    {
        if (u.Id == users.GetUserId(User)) return (IdentityResult.Failed(new IdentityError { Description = "You can't lock your own account." }), "");
        await users.SetLockoutEnabledAsync(u, true);
        var result = await users.SetLockoutEndDateAsync(u, DateTimeOffset.MaxValue);
        await users.UpdateSecurityStampAsync(u);   // signs them out everywhere within minutes
        return (result, "Account locked. They can't sign in until you unlock it.");
    });

    [HttpPost("{id}/unlock"), ValidateAntiForgeryToken]
    public Task<IActionResult> Unlock(string id) => ActAsync(id, async u =>
    {
        await users.ResetAccessFailedCountAsync(u);
        return (await users.SetLockoutEndDateAsync(u, null), "Account unlocked.");
    });

    [HttpPost("{id}/reset"), ValidateAntiForgeryToken]
    public Task<IActionResult> SendReset(string id) => ActAsync(id, async u =>
    {
        await emails.SendPasswordResetAsync(u);
        return (IdentityResult.Success, $"Password reset link sent to {u.Email}.");
    });

    [HttpPost("{id}/resend"), ValidateAntiForgeryToken]
    public Task<IActionResult> ResendConfirmation(string id) => ActAsync(id, async u =>
    {
        await emails.SendConfirmationAsync(u);
        return (IdentityResult.Success, $"Confirmation email sent to {u.Email}.");
    });

    [HttpPost("{id}/superadmin"), ValidateAntiForgeryToken]
    public Task<IActionResult> ToggleSuperAdmin(string id) => ActAsync(id, async u =>
    {
        if (await users.IsInRoleAsync(u, AppRoles.SuperAdmin))
        {
            if (u.Id == users.GetUserId(User)) return (IdentityResult.Failed(new IdentityError { Description = "You can't remove your own platform admin access." }), "");
            return (await users.RemoveFromRoleAsync(u, AppRoles.SuperAdmin), "Platform admin access removed.");
        }
        return (await users.AddToRoleAsync(u, AppRoles.SuperAdmin), "Platform admin access granted.");
    });

    private async Task<IActionResult> ActAsync(string id, Func<ApplicationUser, Task<(IdentityResult Result, string Message)>> action)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        var (result, message) = await action(user);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? message : string.Join(" ", result.Errors.Select(e => e.Description));
        return RedirectToAction(nameof(Index));
    }
}
