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

/// <summary>Development costing: what each project cost, what it earns, and the margin. Owners and developers.</summary>
[RequireFinancials]
public class CostingController(
    IProjectCostService costing,
    IUnitService units,
    IFileStorage storage,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(CancellationToken ct) => View(await costing.GetPortfolioAsync(ct));

    public async Task<IActionResult> Project(int id, CancellationToken ct)
    {
        var o = await costing.GetOverviewAsync(id, ct);
        if (o is null) return NotFound();
        ViewData["CanEdit"] = CanEdit;
        return View(o);
    }

    // ---- Cost ledger ----

    public async Task<IActionResult> Costs(int id, CostKind? kind, CostCategory? category, int? edit, CostKind? add, CancellationToken ct)
    {
        var o = await costing.GetOverviewAsync(id, ct);
        if (o is null) return NotFound();

        var form = new CostForm { AssetId = id, Kind = add ?? CostKind.Actual, Date = clock.TodayEat().ToDateTime(TimeOnly.MinValue) };
        if (edit is int editId && await costing.GetCostAsync(editId, ct) is { } line)
            form = new CostForm
            {
                Id = line.Id, AssetId = id, UnitId = line.UnitId, Kind = line.Kind, Category = line.Category, Description = line.Description,
                Amount = line.Amount, Date = line.Date?.ToDateTime(TimeOnly.MinValue), Supplier = line.Supplier, Reference = line.Reference,
                Notes = line.Notes, HasReceipt = line.HasReceipt
            };
        return View(new CostsPageViewModel { Overview = o, Lines = await costing.ListCostsAsync(id, kind, category, ct), Form = form, Kind = kind, Category = category, CanEdit = CanEdit });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> SaveCost(CostForm form, IFormFile? receipt, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).Distinct());
            return RedirectToAction(nameof(Costs), new { id = form.AssetId, edit = form.Id });
        }
        UploadedFile? upload = null;
        if (receipt is { Length: > 0 })
        {
            var buffer = new MemoryStream();
            await receipt.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            upload = new UploadedFile(buffer, receipt.FileName, receipt.Length);
        }
        var result = await costing.SaveCostAsync(form.Id, form.ToInput(), upload, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{(form.Kind == CostKind.Budget ? "Budget line" : "Cost")} of {form.Amount.Kes()} saved." : result.Error;
        return RedirectToAction(nameof(Costs), new { id = form.AssetId, edit = result.Succeeded ? null : form.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> DeleteCost(int id, int costId, CancellationToken ct)
    {
        var result = await costing.DeleteCostAsync(costId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Cost line removed." : result.Error;
        return RedirectToAction(nameof(Costs), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int costId, CancellationToken ct)
    {
        var r = await costing.GetReceiptAsync(costId, ct);
        if (r is null) return NotFound();
        var stream = await storage.OpenReadAsync(r.Value.Key, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(r.Value.Key), r.Value.FileName);
    }

    // ---- Purchase, allocation and subdivision ----

    [HttpGet]
    public async Task<IActionResult> Setup(int id, CancellationToken ct)
    {
        var o = await costing.GetOverviewAsync(id, ct);
        if (o is null) return NotFound();
        var a = o.Asset;
        return View(new SetupPageViewModel
        {
            Overview = o, CanEdit = CanEdit,
            Form = new ProjectSetupForm
            {
                AssetId = a.Id, AssetName = a.Name, AssetType = a.Type, AcquisitionCost = a.AcquisitionCost,
                AcquisitionDate = a.AcquisitionDate?.ToDateTime(TimeOnly.MinValue), SellerName = a.SellerName, EstimatedLandCost = a.EstimatedLandCost,
                TitleDeedNumber = a.TitleDeedNumber, LandSizeAcres = a.LandSizeAcres, CostAllocation = a.CostAllocation,
                SubdivisionReference = a.SubdivisionReference, SubdivisionApprovedOn = a.SubdivisionApprovedOn?.ToDateTime(TimeOnly.MinValue)
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Setup(ProjectSetupForm form, CancellationToken ct)
    {
        var result = ModelState.IsValid ? await costing.SaveSetupAsync(form.AssetId, form.ToInput(), ct) : Result.Fail("Check the highlighted values.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Project details saved." : result.Error;
        return RedirectToAction(nameof(Setup), new { id = form.AssetId });
    }

    /// <summary>Subdivision: create surveyed plots in one go (uses the normal unit rules, including the plan limit).</summary>
    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> CreatePlots(int id, string prefix, int startNumber, int count, decimal sizeSqm, decimal? salePrice, CancellationToken ct)
    {
        var result = await units.BulkCreateAsync(new BulkUnitInput(id, null, prefix ?? "Plot ", startNumber, count, null, UnitType.Plot,
            sizeSqm > 0 ? sizeSqm : null, null, null, 0m, salePrice), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"{result.Value} plots created." : result.Error;
        return RedirectToAction(nameof(Setup), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> SetTitle(int id, int unitId, string? titleNumber, CancellationToken ct)
    {
        var result = await costing.SetUnitTitleAsync(unitId, titleNumber, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Title number saved." : result.Error;
        return RedirectToAction(nameof(Setup), new { id });
    }
}
