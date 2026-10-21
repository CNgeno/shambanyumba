using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Write operations only; units are viewed on the property page.</summary>
[Authorize(Policy = Policies.CanEdit)]
public class UnitsController(IUnitService units, IAssetService assets) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Create(int assetId, int? nodeId, CancellationToken ct)
    {
        var model = new UnitFormViewModel { AssetId = assetId, StructureNodeId = nodeId, Type = UnitType.Apartment };
        return await FillAsync(model, ct) ? View("Form", model) : NotFound();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitFormViewModel model, string? next, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await FillAsync(model, ct) ? View("Form", model) : NotFound();

        var result = await units.CreateAsync(model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await FillAsync(model, ct) ? View("Form", model) : NotFound();
        }

        TempData["Success"] = $"Unit {model.UnitNumber} added.";
        return next == "another"
            ? RedirectToAction(nameof(Create), new { assetId = model.AssetId, nodeId = model.StructureNodeId })
            : RedirectToAction("Details", "Properties", new { id = model.AssetId });
    }

    [HttpGet]
    public async Task<IActionResult> Bulk(int assetId, int? nodeId, CancellationToken ct)
    {
        var model = new BulkUnitsViewModel { AssetId = assetId, StructureNodeId = nodeId };
        return await FillAsync(model, ct) ? View(model) : NotFound();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Bulk(BulkUnitsViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await FillAsync(model, ct) ? View(model) : NotFound();

        var result = await units.BulkCreateAsync(model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await FillAsync(model, ct) ? View(model) : NotFound();
        }

        TempData["Success"] = $"{result.Value} units added.";
        return RedirectToAction("Details", "Properties", new { id = model.AssetId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var u = await units.GetAsync(id, ct);
        if (u is null) return NotFound();

        var model = new UnitFormViewModel
        {
            Id = u.Id, AssetId = u.AssetId, StructureNodeId = u.StructureNodeId, UnitNumber = u.UnitNumber,
            FloorNumber = u.FloorNumber, Type = u.Type, SizeSqm = u.SizeSqm, Bedrooms = u.Bedrooms,
            Bathrooms = u.Bathrooms, MonthlyRent = u.MonthlyRent, SalePrice = u.SalePrice,
            GracePeriodDays = u.GracePeriodDays, LatePenaltyAmount = u.LatePenaltyAmount, Notes = u.Notes
        };
        return await FillAsync(model, ct) ? View("Form", model) : NotFound();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UnitFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return await FillAsync(model, ct) ? View("Form", model) : NotFound();

        var result = await units.UpdateAsync(id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await FillAsync(model, ct) ? View("Form", model) : NotFound();
        }

        TempData["Success"] = $"Unit {model.UnitNumber} saved.";
        return RedirectToAction("Details", "Properties", new { id = model.AssetId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, UnitStatus status, string? returnUrl, CancellationToken ct)
    {
        var result = await units.ChangeStatusAsync(id, status, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"Unit marked {status.ToString().ToLowerInvariant()}." : result.Error;
        return LocalRedirectOrProperty(returnUrl, id, ct);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl, CancellationToken ct)
    {
        var unit = await units.GetAsync(id, ct);
        var result = await units.DeleteAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"Unit {unit?.UnitNumber} deleted." : result.Error;
        return unit is null ? RedirectToAction("Index", "Properties") : RedirectToAction("Details", "Properties", new { id = unit.AssetId });
    }

    // ---------------------------------------------------------------------

    private IActionResult LocalRedirectOrProperty(string? returnUrl, int unitId, CancellationToken ct)
        => !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Properties");

    private async Task<bool> FillAsync(UnitFormViewModel model, CancellationToken ct)
    {
        var details = await assets.GetDetailsAsync(model.AssetId, ct);
        if (details is null) return false;
        model.AssetName = details.Asset.Name;
        model.NodeOptions = details.Tree.Select(r => new NodeOption(r.Id, r.Path)).ToList();
        return true;
    }

    private async Task<bool> FillAsync(BulkUnitsViewModel model, CancellationToken ct)
    {
        var details = await assets.GetDetailsAsync(model.AssetId, ct);
        if (details is null) return false;
        model.AssetName = details.Asset.Name;
        model.NodeOptions = details.Tree.Select(r => new NodeOption(r.Id, r.Path)).ToList();
        model.Usage = await units.GetUsageAsync(ct);
        return true;
    }
}
