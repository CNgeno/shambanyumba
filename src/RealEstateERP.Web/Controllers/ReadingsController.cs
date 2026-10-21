using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-06-B. Meter readings: a sheet per property and utility, plus the unbilled list.</summary>
public class ReadingsController(
    IChargeService charges,
    IAssetService assets,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    public async Task<IActionResult> Index(bool all = false, int? assetId = null, CancellationToken ct = default)
        => View(new ReadingsIndexViewModel
        {
            Readings = await charges.ListReadingsAsync(!all, assetId, ct),
            Assets = await assets.ListAsync(ct),
            UtilityCharges = (await charges.ListDefinitionsAsync(false, ct)).Where(c => c.Type == ChargeType.Utility).ToList(),
            UnbilledOnly = !all,
            AssetId = assetId,
            CanEdit = currentUser.Role is not (BusinessRole.Viewer or null)
        });

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Sheet(int? assetId, int? chargeId, DateTime? date, CancellationToken ct)
    {
        var model = new MeterSheetViewModel
        {
            Assets = await assets.ListAsync(ct),
            UtilityCharges = (await charges.ListDefinitionsAsync(false, ct)).Where(c => c.Type == ChargeType.Utility).ToList(),
            AssetId = assetId,
            ChargeId = chargeId,
            ReadingDate = date ?? clock.TodayEat().ToDateTime(TimeOnly.MinValue)
        };
        model.ChargeId ??= model.UtilityCharges.FirstOrDefault()?.Id;
        model.AssetId ??= model.Assets.FirstOrDefault()?.Id;
        model.UnitOfMeasure = model.UtilityCharges.FirstOrDefault(c => c.Id == model.ChargeId)?.UnitOfMeasure;

        if (model.AssetId is int a && model.ChargeId is int c)
            model.Rows = await charges.GetMeterSheetAsync(a, c, DateOnly.FromDateTime(model.ReadingDate), ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Sheet(MeterSheetPost post, CancellationToken ct)
    {
        var result = await charges.SaveReadingsAsync(post.ChargeId, DateOnly.FromDateTime(post.ReadingDate),
            post.Rows.Select(r => new MeterReadingEntry(r.UnitId, r.Reading, r.IsOpening)).ToList(), ct);

        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{result.Value} reading(s) saved. They'll be billed on the next invoice run."
            : result.Error;
        return RedirectToAction(nameof(Sheet), new { assetId = post.AssetId, chargeId = post.ChargeId, date = post.ReadingDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await charges.DeleteReadingAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Reading deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
