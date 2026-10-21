using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>An estate's setup actions: its charges, photos and description. Pages live on the property page.</summary>
public class EstateController(IEstateService estate, IUnitService units, IFileStorage storage) : Controller
{
    private IActionResult Back(int assetId, Result result, string ok, string anchor)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? ok : result.Error;
        return Redirect(Url.Action("Details", "Properties", new { id = assetId }) + "#" + anchor);
    }

    // ---- charges ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddCharge(int assetId, int chargeDefinitionId, decimal? amount, decimal? unitRate, CancellationToken ct)
        => Back(assetId, await estate.AddChargeAsync(assetId, new PropertyChargeInput(chargeDefinitionId, amount, unitRate), ct),
            "Charge added to the estate. New leases here get it automatically.", "estate-charges");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemoveCharge(int assetId, int id, CancellationToken ct)
        => Back(assetId, await estate.RemoveChargeAsync(assetId, id, ct), "Removed from the estate. Leases that have it keep it until you remove it there.", "estate-charges");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> ApplyCharges(int assetId, string? returnUrl, CancellationToken ct)
    {
        var result = await estate.ApplyChargesToLeasesAsync(assetId, ct);
        var message = result.Succeeded ? (result.Value == 0 ? "Every lease already has the estate's charges." : $"{result.Value} charge(s) added to current leases.") : result.Error;
        TempData[result.Succeeded ? "Success" : "Error"] = message;
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : Redirect(Url.Action("Details", "Properties", new { id = assetId }) + "#estate-charges");
    }

    // ---- estate photos and description ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit), RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> AddPhotos(int assetId, List<IFormFile> files, CancellationToken ct)
        => Back(assetId, await estate.AddEstatePhotosAsync(assetId, await ReadAsync(files, ct), ct), "Photos added.", "estate-photos");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemovePhoto(int assetId, int photoId, CancellationToken ct)
        => Back(assetId, await estate.RemoveEstatePhotoAsync(assetId, photoId, ct), "Photo removed.", "estate-photos");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Description(int assetId, string? description, CancellationToken ct)
        => Back(assetId, await estate.SaveDescriptionAsync(assetId, description, ct), "Description saved.", "estate-photos");

    // ---- house interiors ----

    [HttpGet]
    public async Task<IActionResult> UnitPhotos(int unitId, CancellationToken ct)
    {
        var unit = await units.GetAsync(unitId, ct);
        if (unit is null) return NotFound();
        return View(new UnitPhotosModel(unit.Id, unit.UnitNumber, unit.AssetId, unit.Asset?.Name ?? "Property", await estate.GetUnitPhotosAsync(unitId, ct),
            User.HasClaim(c => c.Type == RealEstateERP.Core.Constants.AppClaimTypes.BusinessRole && c.Value != "Viewer")));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit), RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> UnitPhotos(int unitId, List<IFormFile> files, CancellationToken ct)
    {
        var result = await estate.AddUnitPhotosAsync(unitId, await ReadAsync(files, ct), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Photos added." : result.Error;
        return RedirectToAction(nameof(UnitPhotos), new { unitId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemoveUnitPhoto(int unitId, int photoId, CancellationToken ct)
    {
        var result = await estate.RemoveUnitPhotoAsync(unitId, photoId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Photo removed." : result.Error;
        return RedirectToAction(nameof(UnitPhotos), new { unitId });
    }

    // ---- serving photos (this business only) ----

    [HttpGet, ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Photo(string kind, int id, CancellationToken ct)
    {
        var key = await estate.GetPhotoKeyAsync(kind, id, ct);
        if (key is null) return NotFound();
        var stream = await storage.OpenReadAsync(key, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(key));
    }

    private static async Task<List<UploadedFile>> ReadAsync(List<IFormFile> files, CancellationToken ct)
    {
        var uploads = new List<UploadedFile>();
        foreach (var f in files.Where(f => f.Length > 0))
        {
            var buffer = new MemoryStream();
            await f.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            uploads.Add(new UploadedFile(buffer, f.FileName, f.Length));
        }
        return uploads;
    }
}
