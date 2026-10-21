using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Verified enquiries from the public Properties pages.</summary>
public class LeadsController(IListingService listings) : Controller
{
    public async Task<IActionResult> Index(LeadStage? stage, CancellationToken ct)
    {
        ViewData["Stage"] = stage;
        ViewData["Awaiting"] = await listings.GetAwaitingConfirmationAsync(null, ct);
        return View(await listings.GetLeadsAsync(stage, ct));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Unlock(int id, CancellationToken ct) => Done(await listings.UnlockAsync(id, ct), "Lead opened.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Update(int id, LeadStage stage, string? notes, CancellationToken ct) => Done(await listings.UpdateLeadAsync(id, stage, notes, ct), "Lead updated.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Refund(int id, string reason, CancellationToken ct) => Done(await listings.RefundAsync(id, reason, ct), "Lead reported. The credits are back in your balance.");

    private IActionResult Done(Result result, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
