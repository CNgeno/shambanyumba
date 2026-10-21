using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-12 business side: advertise units on the public Properties pages.</summary>
public class ListingsController(IListingService listings, IFileStorage storage, ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(ListingStatus? status, CancellationToken ct)
    {
        ViewData["CanEdit"] = CanEdit;
        ViewData["Status"] = status;
        return View(await listings.ListAsync(status, ct));
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int? unitId, ListingType type = ListingType.Rent, CancellationToken ct = default)
    {
        var form = new ListingForm { UnitId = unitId, Type = type, Units = await listings.GetListableUnitsAsync(ct) };
        if (form.Units.FirstOrDefault(u => u.Id == unitId) is { } unit)
        {
            form.Title = type == ListingType.Sale ? unit.SuggestedSaleTitle : unit.SuggestedRentTitle;
            form.Price = type == ListingType.Sale ? unit.SalePrice ?? 0 : unit.MonthlyRent;
        }
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(ListingForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await listings.CreateAsync(form.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Listing saved as a draft. Add photos, then publish it.";
                return RedirectToAction(nameof(Edit), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        form.Units = await listings.GetListableUnitsAsync(ct);
        return View(form);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await listings.GetAsync(id, ct);
        if (d is null) return NotFound();
        var l = d.Listing;
        return View(new ListingEditViewModel
        {
            Details = d, CanEdit = CanEdit,
            Form = new ListingForm { Id = l.Id, UnitId = l.UnitId, Type = l.Type, Title = l.Title, Description = l.Description, Price = l.Price }
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, ListingForm form, CancellationToken ct)
    {
        var result = ModelState.IsValid ? await listings.UpdateAsync(id, form.ToInput(), ct) : Result.Fail("Check the title (10+ characters), description (40+) and price.");
        return Done(result, id, "Listing saved.");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit), RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Photos(int id, List<IFormFile> files, CancellationToken ct)
    {
        var uploads = new List<UploadedFile>();
        foreach (var f in files.Where(f => f.Length > 0))
        {
            var buffer = new MemoryStream();
            await f.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            uploads.Add(new UploadedFile(buffer, f.FileName, f.Length));
        }
        return Done(await listings.AddPhotosAsync(id, uploads, ct), id, $"{uploads.Count} photo(s) added.");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemovePhoto(int id, int photoId, CancellationToken ct) => Done(await listings.RemovePhotoAsync(id, photoId, ct), id, "Photo removed.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> MakeCover(int id, int photoId, CancellationToken ct) => Done(await listings.MakeCoverAsync(id, photoId, ct), id, "Cover photo changed.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> SetStatus(int id, ListingStatus status, CancellationToken ct)
        => Done(await listings.SetStatusAsync(id, status, ct), id, status switch
        {
            ListingStatus.Published => "Listing published. It shows on the Properties page while the unit is available.",
            ListingStatus.Paused => "Listing paused: hidden from the public until you publish it again.",
            ListingStatus.Closed => "Listing closed.",
            _ => "Saved."
        });

    /// <summary>Photo preview for the business (drafts included).</summary>
    [HttpGet]
    public async Task<IActionResult> Photo(int photoId, CancellationToken ct)
    {
        var p = await listings.GetPhotoAsync(photoId, ct);
        if (p is null) return NotFound();
        var stream = await storage.OpenReadAsync(p.StorageKey, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(p.StorageKey));
    }

    private IActionResult Done(Result result, int id, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Edit), new { id });
    }
}
