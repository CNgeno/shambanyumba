using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

/// <summary>Accepting an invitation from the email link, or from the business selection page.</summary>
[AllowWithoutBusiness, SkipSubscriptionCheck]
public class InvitationsController(
    ITeamService team,
    IBusinessService businesses,
    UserManager<ApplicationUser> users,
    BusinessContextSignIn businessSignIn) : Controller
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Accept(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return NotFound();
        var signedIn = User.Identity?.IsAuthenticated == true;
        return View(new InvitationPageViewModel
        {
            Token = token,
            Info = await team.DescribeTokenAsync(token, ct),
            SignedIn = signedIn,
            SignedInEmail = signedIn ? User.Identity!.Name : null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(string token, bool confirm, CancellationToken ct)
        => await JoinAsync(await team.AcceptByTokenAsync(token, users.GetUserId(User)!, ct), () => RedirectToAction(nameof(Accept), new { token }), ct);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptPending(int id, CancellationToken ct)
        => await JoinAsync(await team.AcceptByIdAsync(id, users.GetUserId(User)!, ct), () => RedirectToAction("Select", "Business"), ct);

    private async Task<IActionResult> JoinAsync(Core.Common.Result<int> result, Func<IActionResult> onError, CancellationToken ct)
    {
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return onError();
        }
        var user = await users.GetUserAsync(User);
        var membership = user is null ? null : await businesses.GetMembershipAsync(user.Id, result.Value, ct);
        if (user is null || membership is null) return RedirectToAction("Select", "Business");

        await businessSignIn.SelectBusinessAsync(user, membership);
        TempData["Success"] = $"Welcome to {membership.BusinessName}.";
        return RedirectToAction("Index", "Dashboard");
    }
}
