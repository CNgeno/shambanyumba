using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/credits")]
public class AdminCreditsController(ICreditService creditService, UserManager<ApplicationUser> users) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(new AdminCreditsViewModel { Pending = await creditService.GetPendingPurchasesAsync(ct) });

    [HttpPost("{id:int}/approve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, CancellationToken ct)
    {
        var result = await creditService.ApprovePurchaseAsync(id, users.GetUserId(User)!, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Credits added to the business." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/reject"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken ct)
    {
        var result = await creditService.RejectPurchaseAsync(id, users.GetUserId(User)!, reason, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Purchase rejected." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("adjust"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int businessId, int credits, string reason, CancellationToken ct)
    {
        var result = await creditService.AdjustAsync(businessId, credits, reason, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"Business #{businessId} adjusted by {credits:+#;-#} credits." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
