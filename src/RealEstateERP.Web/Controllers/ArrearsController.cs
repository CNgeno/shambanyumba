using RealEstateERP.Web.Formatting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-09: arrears aging, late penalties and collections notes.</summary>
public class ArrearsController(
    IArrearsService arrears,
    IAssetService assets,
    ISystemConfigService config,
    ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(int? assetId, CollectionStage? stage, CancellationToken ct)
    {
        var report = await arrears.GetReportAsync(assetId, ct);
        if (stage is CollectionStage s)
            report = report with { Tenants = report.Tenants.Where(t => t.Stage == s).ToList() };

        return View(new ArrearsIndexViewModel
        {
            Report = report,
            Assets = (await assets.ListAsync(ct)).ToLet(),
            AssetId = assetId,
            Stage = stage,
            PenaltiesDue = (await arrears.PreviewPenaltiesAsync(ct)).Count,
            CanEdit = CanEdit
        });
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Penalties(CancellationToken ct)
        => View(new PenaltiesViewModel
        {
            Candidates = await arrears.PreviewPenaltiesAsync(ct),
            DefaultGraceDays = await config.GetIntAsync(ConfigKeys.DefaultGraceDaysAfterDue, 5, ct),
            DefaultPercent = await config.GetDecimalAsync(ConfigKeys.DefaultPenaltyPercent, 5m, ct)
        });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Penalties(List<int>? invoiceIds, CancellationToken ct)
    {
        if (invoiceIds is not { Count: > 0 })
        {
            TempData["Error"] = "Tick at least one invoice to penalise.";
            return RedirectToAction(nameof(Penalties));
        }
        var result = await arrears.ApplyPenaltiesAsync(invoiceIds, ct);
        TempData[result.Created > 0 ? "Success" : "Error"] = $"{result.Created} penalty invoice(s) issued, totalling KES {result.Total:N2}."
            + (result.Problems.Count > 0 ? " " + string.Join(" ", result.Problems.Take(3)) : "");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddNote(CollectionNoteForm form, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await arrears.AddNoteAsync(new CollectionNoteInput(form.TenantId, form.Stage, form.Note,
                form.PromisedPaymentDate is DateTime d ? DateOnly.FromDateTime(d) : null), form.DocumentId, ct)
            : Core.Common.Result.Fail("Write what happened.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Collections note saved." : result.Error;
        return Redirect(Url.Action("Details", "Tenants", new { id = form.TenantId }) + "#collections");
    }
}
