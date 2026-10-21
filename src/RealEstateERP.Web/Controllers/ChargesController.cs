using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Charge library + attaching charges to leases.</summary>
public class ChargesController(IChargeService charges, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["CanAdmin"] = currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;
        return View(await charges.ListDefinitionsAsync(includeInactive: true, ct));
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public IActionResult Create(ChargeType type = ChargeType.Recurring) => View("Form", new ChargeFormViewModel { Type = type });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Create(ChargeFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", model);
        var result = await charges.CreateDefinitionAsync(model.ToInput(), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View("Form", model); }
        TempData["Success"] = $"{model.Name} added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await charges.GetDefinitionAsync(id, ct);
        if (d is null) return NotFound();
        return View("Form", new ChargeFormViewModel
        {
            Id = d.Id, Name = d.Name, Type = d.Type, DefaultAmount = d.DefaultAmount, UnitRate = d.UnitRate,
            UnitOfMeasure = d.UnitOfMeasure, TaxPercent = d.TaxPercent, Description = d.Description, IsActive = d.IsActive,
            Frequency = d.Frequency, AnchorMonth = d.AnchorMonth
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Edit(int id, ChargeFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);
        var result = await charges.UpdateDefinitionAsync(id, model.ToInput(), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View("Form", model); }
        TempData["Success"] = $"{model.Name} saved. Existing invoices are unchanged.";
        return RedirectToAction(nameof(Index));
    }

    // ---- On a lease ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddToLease(int leaseId, int chargeDefinitionId, decimal? amountOverride, decimal? unitRateOverride, CancellationToken ct)
    {
        var result = await charges.AddLeaseChargeAsync(leaseId, new LeaseChargeInput(chargeDefinitionId, amountOverride, unitRateOverride), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Charge added to the lease. It appears on the next invoice." : result.Error;
        return Redirect(Url.Action("Details", "Leases", new { id = leaseId }) + "#billing");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemoveFromLease(int id, int leaseId, CancellationToken ct)
    {
        var result = await charges.RemoveLeaseChargeAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Charge removed. Past invoices are unchanged." : result.Error;
        return Redirect(Url.Action("Details", "Leases", new { id = leaseId }) + "#billing");
    }
}
