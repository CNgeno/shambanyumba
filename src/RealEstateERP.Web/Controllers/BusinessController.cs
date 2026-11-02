using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

[AllowWithoutBusiness]
public class BusinessController(
    IBusinessService businesses,
    ITeamService team,
    UserManager<ApplicationUser> users,
    BusinessContextSignIn businessSignIn, IReferralService referrals, IConfiguration config) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Select(string? returnUrl, CancellationToken ct)
    {
        var userId = users.GetUserId(User)!;
        var list = await businesses.GetBusinessesForUserAsync(userId, ct);
        var invitations = await team.GetInvitationsForUserAsync(userId, ct);
        // Came from another of our sites (e.g. the booking site)? Go back there after choosing.
        var back = TrustedReturnUrl.Check(returnUrl, Url, config);
        ViewData["ReturnUrl"] = back;
        if (list.Count == 0 && invitations.Count == 0)
            return RedirectToAction(nameof(Create), new { returnUrl = back });
        return View(new SelectBusinessViewModel { Businesses = list, Invitations = invitations });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(int businessId, string? returnUrl, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        var membership = await businesses.GetMembershipAsync(user.Id, businessId, ct);
        if (membership is null) return Forbid();

        await businessSignIn.SelectBusinessAsync(user, membership);
        return TrustedReturnUrl.Check(returnUrl, Url, config) is string back ? Redirect(back) : RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl)
    {
        ViewData["ReturnUrl"] = TrustedReturnUrl.Check(returnUrl, Url, config);
        return View(new CreateBusinessViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateBusinessViewModel model, string? returnUrl, CancellationToken ct)
    {
        var back = TrustedReturnUrl.Check(returnUrl, Url, config);
        ViewData["ReturnUrl"] = back;
        if (!ModelState.IsValid) return View(model);

        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        var businessId = await businesses.CreateBusinessAsync(user.Id, new CreateBusinessRequest(
            model.Name, model.Type!.Value, model.Email, model.Phone, model.PhysicalAddress, model.KraPin), ct);

        // Who brought them: the code typed in, else the one remembered from a referral link.
        var code = string.IsNullOrWhiteSpace(model.ReferralCode) ? Request.Cookies["sn_ref"] : model.ReferralCode;
        if (await referrals.AttachAsync(businessId, code, ct)) Response.Cookies.Delete("sn_ref");

        var membership = await businesses.GetMembershipAsync(user.Id, businessId, ct);
        await businessSignIn.SelectBusinessAsync(user, membership!);

        TempData["Success"] = $"{model.Name} is ready.";
        return back is not null ? Redirect(back) : RedirectToAction("Index", "Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch()
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();

        await businessSignIn.ClearBusinessAsync(user);
        return RedirectToAction(nameof(Select));
    }
}
