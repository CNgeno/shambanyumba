using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>
/// Hosts manage their short-stay properties here, without going to ShambaNyumba: add a property (always "Short stays"),
/// its units with a nightly rate (one or many), and photos. Same services and rules as the main site.
/// </summary>
[Authorize, Route("host")]
public class HostPropertiesController(IAssetService assets, IUnitService units, IStayService stays, IEstateService estate,
    ICurrentUserService currentUser, RealEstateERP.Infrastructure.Data.ApplicationDbContext db, IFileStorage storage) : Controller
{
    private bool CanEdit => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin or BusinessRole.User;

    [HttpGet("properties")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (currentUser.BusinessId is null) return Redirect("/host");
        ViewData["CanEdit"] = CanEdit;
        return View(await stays.ListPropertiesAsync(ct));
    }

    [HttpGet("properties/new")]
    public IActionResult New() => CanEdit ? View() : Forbid();

    [HttpPost("properties/new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, AssetType type, string? location, string? county, string? description,
        double? latitude, double? longitude, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await assets.CreateAsync(new AssetInput(name ?? "", type, AssetLifecycle.Operational, location, county,
            null, null, null, null, description, PropertyPurpose.ShortStay, latitude, longitude), ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return View("New"); }
        TempData["Success"] = "Property added. Now add its units with their nightly rate.";
        return Redirect($"/host/properties/{r.Value}");
    }

    /// <summary>The map picker sends pasted Google Maps links here (short share links can only be followed server-side).</summary>
    [HttpGet("maps/resolve")]
    public async Task<IActionResult> ResolveMapLink(string? url, [FromServices] RealEstateERP.Infrastructure.Services.MapLinkResolver resolver, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var pin = await resolver.ResolveAsync(url, ct);
        return pin is { } p ? Json(new { lat = p.Latitude, lng = p.Longitude }) : Json(new { error = "No location found in that link." });
    }

    [HttpPost("properties/{assetId:int}/pin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPin(int assetId, double? latitude, double? longitude, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        if (!await stays.IsShortStayPropertyAsync(assetId, ct)) return NotFound();
        var r = await assets.SetMapPinAsync(assetId, latitude, longitude, ct);
        if (r.Succeeded) TempData["Success"] = latitude is null ? "Map pin removed." : "Map pin saved. Guests can now open it in Google Maps.";
        else TempData["Error"] = r.Error;
        return Redirect($"/host/properties/{assetId}#map");
    }

    [HttpGet("properties/{assetId:int}")]
    public async Task<IActionResult> Details(int assetId, CancellationToken ct)
    {
        if (!await stays.IsShortStayPropertyAsync(assetId, ct)) return NotFound();
        ViewData["CanEdit"] = CanEdit;
        ViewData["Property"] = (await stays.ListPropertiesAsync(ct)).First(p => p.AssetId == assetId);
        ViewData["Photos"] = await estate.GetEstatePhotosAsync(assetId, ct);
        return View(await stays.GetPropertySummaryAsync(assetId, ct));
    }

    [HttpPost("properties/{assetId:int}/units"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUnits(int assetId, string mode, string? unitNumber, string? prefix, int start, int count,
        UnitType type, int? bedrooms, int? bathrooms, decimal nightly, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        if (!await stays.IsShortStayPropertyAsync(assetId, ct)) return NotFound();
        if (nightly <= 0) { TempData["Error"] = "Enter the nightly rate."; return Redirect($"/host/properties/{assetId}#add-units"); }
        // The unit "rent" is the nightly rate on a short-stay property: EnsureStays moves it to the stay.
        var r = mode == "many"
            ? await units.BulkCreateAsync(new BulkUnitInput(assetId, null, prefix ?? "", start, count, null, type, null, bedrooms, bathrooms, nightly), ct)
            : await units.CreateAsync(new UnitInput(assetId, null, unitNumber ?? "", null, type, null, bedrooms, bathrooms, nightly, null, null, null, null), ct);
        if (!r.Succeeded) { TempData["Error"] = r.Error; return Redirect($"/host/properties/{assetId}#add-units"); }
        await stays.EnsureStaysAsync(assetId, ct);
        TempData["Success"] = mode == "many" ? $"{r.Value} units added as short stays (drafts). Add photos, then publish." : "Unit added as a short stay (draft). Add photos, then publish.";
        return Redirect($"/host/properties/{assetId}");
    }

    [HttpPost("properties/{assetId:int}/photos"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPropertyPhotos(int assetId, List<IFormFile> files, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await estate.AddEstatePhotosAsync(assetId, await ReadAsync(files, ct), ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Photos added." : r.Error;
        return Redirect($"/host/properties/{assetId}#photos");
    }

    [HttpPost("properties/{assetId:int}/photos/{photoId:int}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePropertyPhoto(int assetId, int photoId, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await estate.RemoveEstatePhotoAsync(assetId, photoId, ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Photo removed." : r.Error;
        return Redirect($"/host/properties/{assetId}#photos");
    }

    [HttpGet("units/{unitId:int}/photos")]
    public async Task<IActionResult> UnitPhotos(int unitId, CancellationToken ct)
    {
        var stay = (await stays.ListAsync(ct)).FirstOrDefault(s => s.UnitId == unitId);
        if (stay is null) return NotFound();
        ViewData["CanEdit"] = CanEdit;
        ViewData["Stay"] = stay;
        return View(await estate.GetUnitPhotosAsync(unitId, ct));
    }

    [HttpPost("units/{unitId:int}/photos"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUnitPhotos(int unitId, List<IFormFile> files, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await estate.AddUnitPhotosAsync(unitId, await ReadAsync(files, ct), ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Photos added." : r.Error;
        return Redirect($"/host/units/{unitId}/photos");
    }

    [HttpPost("units/{unitId:int}/photos/{photoId:int}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveUnitPhoto(int unitId, int photoId, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await estate.RemoveUnitPhotoAsync(unitId, photoId, ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Photo removed." : r.Error;
        return Redirect($"/host/units/{unitId}/photos");
    }

    /// <summary>The host's own photos (drafts included). The business filter keeps every host to their own.</summary>
    [HttpGet("photo/{kind}/{id:int}")]
    public async Task<IActionResult> Photo(string kind, int id, CancellationToken ct)
    {
        var photo = kind == "unit"
            ? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.UnitPhotos.Where(p => p.Id == id).Select(p => new { p.StorageKey, p.FileName }), ct)
            : await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.AssetPhotos.Where(p => p.Id == id).Select(p => new { p.StorageKey, p.FileName }), ct);
        if (photo is null) return NotFound();
        var stream = await storage.OpenReadAsync(photo.StorageKey, ct);
        if (stream is null) return NotFound();
        var type = Path.GetExtension(photo.FileName).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", _ => "image/jpeg" };
        return File(stream, type);
    }

    private static async Task<List<UploadedFile>> ReadAsync(List<IFormFile>? files, CancellationToken ct)
    {
        var uploads = new List<UploadedFile>();
        foreach (var f in (files ?? []).Where(f => f.Length > 0))
        {
            var buffer = new MemoryStream();
            await f.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            uploads.Add(new UploadedFile(buffer, f.FileName, f.Length));
        }
        return uploads;
    }
}
