using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Middleware;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-01-E. Business profile, logo and team. Reachable while suspended so owners can still manage access.</summary>
[Authorize(Policy = Policies.BusinessAdmin), SkipSubscriptionCheck]
public class BusinessSettingsController(
    IBusinessService businesses,
    ITeamService team,
    IFileStorage storage,
    IMemoryCache cache,
    Microsoft.AspNetCore.Identity.UserManager<RealEstateERP.Infrastructure.Identity.ApplicationUser> users,
    RealEstateERP.Web.Services.BusinessContextSignIn businessSignIn,
    IAutomationService automation,
    IReferralService referrals,
    ICurrentUserService currentUser) : Controller
{
    private int BusinessId => currentUser.BusinessId!.Value;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var p = await businesses.GetProfileAsync(BusinessId, ct);
        if (p is null) return NotFound();
        var membership = (await businesses.GetBusinessesForUserAsync(currentUser.UserId!, ct)).FirstOrDefault(b => b.BusinessId == BusinessId);
        if (!RealEstateERP.Core.Constants.DemoUsers.IsDemo(User.Identity?.Name))   // demo businesses are wiped nightly: no referral codes
            ViewData["Referral"] = await referrals.GetCustomerReferralAsync(BusinessId, ct);
        return View(new BusinessProfileForm
        {
            Name = p.Name, ManagesForOthers = (membership?.Type ?? BusinessType.Owner) == BusinessType.Agency, Email = p.Email, Phone = p.Phone,
            PhysicalAddress = p.Address, KraPin = p.KraPin, HasLogo = p.LogoKey is not null, ShowRentalTools = p.RentalToolsEnabled
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BusinessProfileForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);
        var result = await businesses.UpdateProfileAsync(BusinessId, form.ToInput(), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View(form); }

        // Re-issue the business claims so the new name and type (menus) apply straight away.
        var user = await users.GetUserAsync(User);
        var membership = user is null ? null : await businesses.GetMembershipAsync(user.Id, BusinessId, ct);
        if (user is not null && membership is not null) await businessSignIn.SelectBusinessAsync(user, membership);
        TempData["Success"] = "Business details saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> Logo(IFormFile? file, bool remove, CancellationToken ct)
    {
        UploadedFile? upload = null;
        if (!remove)
        {
            if (file is not { Length: > 0 }) { TempData["Error"] = "Choose an image to upload."; return RedirectToAction(nameof(Index)); }
            var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            upload = new UploadedFile(buffer, file.FileName, file.Length);
        }
        var result = await businesses.SetLogoAsync(BusinessId, upload, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? (remove ? "Logo removed." : "Logo saved. It now appears on your PDFs.") : result.Error;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The selected business's logo, for any signed-in member (the class requires Owner/Admin, so this opts out of that).
    /// Only ever serves the logo of the business in the user's own cookie.
    /// </summary>
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> LogoImage(CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.BusinessId is not int id) return NotFound();
        var key = (await businesses.GetProfileAsync(id, ct))?.LogoKey;
        if (key is null) return NotFound();
        var stream = await storage.OpenReadAsync(key, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(key));
    }

    // ---- Automation ----

    [HttpGet]
    public async Task<IActionResult> Automation(CancellationToken ct)
    {
        var status = await automation.GetStatusAsync(ct);
        var st = status.Settings;
        return View(new AutomationViewModel
        {
            Status = status,
            Form = new AutomationForm
            {
                AutoInvoiceEnabled = st.AutoInvoiceEnabled, AutoInvoiceDay = st.AutoInvoiceDay, AutoPenaltiesEnabled = st.AutoPenaltiesEnabled,
                PenaltyGraceDays = st.PenaltyGraceDays, PenaltyPercent = st.PenaltyPercent, EmailInvoicesToTenants = st.EmailInvoicesToTenants,
                EmailReceiptsToTenants = st.EmailReceiptsToTenants, PaymentInstructions = st.PaymentInstructions, AutoListVacantUnits = st.AutoListVacantUnits
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Automation(AutomationForm form, CancellationToken ct)
    {
        var result = ModelState.IsValid ? await automation.SaveSettingsAsync(form.ToSettings(), ct) : Core.Common.Result.Fail("Check the highlighted values.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Automation settings saved. The daily job picks them up within the hour." : result.Error;
        return RedirectToAction(nameof(Automation));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAutomationNow(CancellationToken ct)
    {
        var result = await automation.RunAsync(ct);
        TempData[result.Ran ? "Success" : "Error"] = result.Summary;
        return RedirectToAction(nameof(Automation));
    }

    // ---- Team ----

    [HttpGet]
    public async Task<IActionResult> Team(CancellationToken ct)
        => View(new TeamViewModel
        {
            Members = await team.GetMembersAsync(ct),
            Invitations = await team.GetOpenInvitationsAsync(ct),
            IsOwner = currentUser.Role == BusinessRole.Owner,
            BusinessType = currentUser.BusinessType
        });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string email, BusinessRole role, CancellationToken ct)
        => TeamResult(await team.InviteAsync(email, role, AcceptUrlFormat(), ct), $"Invitation sent to {email}.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(int id, CancellationToken ct)
        => TeamResult(await team.ResendInvitationAsync(id, AcceptUrlFormat(), ct), "Invitation sent again. The earlier link no longer works.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
        => TeamResult(await team.RevokeInvitationAsync(id, ct), "Invitation cancelled.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetFinances(int id, bool canView, CancellationToken ct)
        => TeamResult(await team.SetFinancialAccessAsync(id, canView, ct), canView ? "They can now see finances." : "Finances are now hidden from them.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int id, BusinessRole role, CancellationToken ct)
    {
        var userId = (await team.GetMembersAsync(ct)).FirstOrDefault(m => m.MembershipId == id)?.UserId;
        var result = await team.ChangeRoleAsync(id, role, ct);
        if (result.Succeeded && userId is not null) cache.Remove(TenantResolutionMiddleware.MembershipCacheKey(userId, BusinessId));
        return TeamResult(result, $"Role changed to {role}. It takes effect on their next page.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var userId = (await team.GetMembersAsync(ct)).FirstOrDefault(m => m.MembershipId == id)?.UserId;
        var result = await team.RemoveAsync(id, ct);
        if (result.Succeeded && userId is not null) cache.Remove(TenantResolutionMiddleware.MembershipCacheKey(userId, BusinessId));
        if (result.Succeeded && userId == currentUser.UserId) return RedirectToAction("Select", "Business");
        return TeamResult(result, "Member removed. They no longer have access.");
    }

    private IActionResult TeamResult(Core.Common.Result result, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Team));
    }

    private string AcceptUrlFormat()
        => Url.Action("Accept", "Invitations", new { token = "TOKEN_PLACEHOLDER" }, Request.Scheme)!.Replace("TOKEN_PLACEHOLDER", "{0}");
}
