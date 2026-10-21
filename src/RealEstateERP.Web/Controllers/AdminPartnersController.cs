using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Platform admin: influencers, business developers and customers who refer businesses, and their commission.</summary>
[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/partners")]
public class AdminPartnersController(IReferralService referrals) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await referrals.ListPartnersAsync(ct));

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, PartnerKind kind, string? code, string? phone, string? email, decimal commission, string? notes, CancellationToken ct)
    {
        var result = await referrals.CreatePartnerAsync(new PartnerInput(name, kind, code, phone, email, commission, true, notes), ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }
        TempData["Success"] = "Partner added. Share their link with them.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
        => await referrals.GetPartnerAsync(id, ct) is { } p ? View(p) : NotFound();

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, string name, PartnerKind kind, string? phone, string? email, decimal commission, bool isActive, string? notes, CancellationToken ct)
    {
        var result = await referrals.UpdatePartnerAsync(id, new PartnerInput(name, kind, null, phone, email, commission, isActive, notes), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Saved." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("commission/{commissionId:int}/paid"), ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int commissionId, int partnerId, string reference, CancellationToken ct)
    {
        var result = await referrals.MarkCommissionPaidAsync(commissionId, reference, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Commission marked as paid." : result.Error;
        return RedirectToAction(nameof(Details), new { id = partnerId });
    }
}
