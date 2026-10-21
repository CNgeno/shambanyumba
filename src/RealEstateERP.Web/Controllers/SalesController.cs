using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

/// <summary>FR-11. Developer and agency businesses.</summary>
[RequireCapability(Capability.Sales)]
public class SalesController(
    ISaleService sales,
    IAgencyService agency,
    IAgentScopeService agentScope,
    ICreditService credits,
    IPdfService pdf,
    BusinessBranding branding,
    IFileStorage storage,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);
    private bool CanAdmin => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    public async Task<IActionResult> Index(SalesListView view = SalesListView.Open, string? q = null, CancellationToken ct = default)
        => View(new SalesIndexViewModel
        {
            Sales = await sales.ListAsync(view, null, null, q, ct),
            Totals = await sales.GetTotalsAsync(ct),
            View = view, Search = q, CanEdit = CanEdit
        });

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var d = await sales.GetAsync(id, ct);
        if (d is null) return NotFound();
        var isAgent = (await agentScope.GetAsync(ct)).IsRestricted;
        return View(new SaleDetailsViewModel { Details = d, CanEdit = CanEdit, CanAdmin = CanAdmin && !isAgent, Today = clock.TodayEat() });
    }

    // ---- New sale ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int? buyerId, int? unitId, CancellationToken ct)
    {
        var today = clock.TodayEat();
        var form = new SaleForm { BuyerId = buyerId, UnitId = unitId, SaleDate = today.ToDateTime(TimeOnly.MinValue), FirstInstalmentDate = today.AddMonths(1).ToDateTime(TimeOnly.MinValue) };
        await FillAsync(form, ct);
        if (unitId is int u && form.Units.FirstOrDefault(x => x.Id == u)?.SalePrice is decimal price)
        {
            form.Price = price;
            form.DepositAmount = Math.Round(price * 0.2m, 0);
        }
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(SaleForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await sales.CreateAsync(form.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Sale recorded and the unit reserved. Complete the sale agreement step once it's signed.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await FillAsync(form, ct);
        return View(form);
    }

    /// <summary>Live schedule preview for the new-sale form (no saving).</summary>
    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> SchedulePreview([FromQuery] SaleForm form, CancellationToken ct)
    {
        if (form.SaleDate is null || form.Price <= 0) return PartialView("_SchedulePreview", Array.Empty<ScheduleLine>());
        var input = new SaleInput(0, 0, null, DateOnly.FromDateTime(form.SaleDate.Value), form.Price, form.PaymentPlan, form.DepositAmount,
            form.InstalmentCount, form.InstalmentFrequency, form.FirstInstalmentDate is DateTime f ? DateOnly.FromDateTime(f) : null, 0, null);
        ViewData["Error"] = SalesRules.ValidatePlan(input.Price, input.DepositAmount, input.PaymentPlan, input.InstalmentCount, input.SaleDate, input.FirstInstalmentDate);
        return PartialView("_SchedulePreview", await sales.PreviewScheduleAsync(input, ct));
    }

    // ---- Payments ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RecordPayment(int id, DateTime paymentDate, decimal amount, PaymentMethod method, string? reference, string? notes, CancellationToken ct)
    {
        var result = await sales.RecordPaymentAsync(id, new SalePaymentInput(DateOnly.FromDateTime(paymentDate), amount, method, reference, notes), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"Payment of {amount.Kes()} recorded." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> ReversePayment(int id, int paymentId, string reason, CancellationToken ct)
        => Done(await sales.ReversePaymentAsync(paymentId, reason, ct), id, "Payment reversed. The schedule and balance are updated.");

    // ---- Milestones ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit), RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> CompleteMilestone(int id, MilestoneType type, DateTime completedOn, string? reference, string? notes, IFormFile? document, CancellationToken ct)
    {
        UploadedFile? upload = null;
        if (document is { Length: > 0 })
        {
            var buffer = new MemoryStream();
            await document.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            upload = new UploadedFile(buffer, document.FileName, document.Length);
        }
        var message = type switch
        {
            MilestoneType.Agreement => "Agreement recorded. The sale is active and the unit is marked sold.",
            MilestoneType.Handover => "Handover recorded.",
            _ => $"{type.Display()} recorded."
        };
        return Done(await sales.CompleteMilestoneAsync(id, type, DateOnly.FromDateTime(completedOn), reference, notes, upload, ct), id, message);
    }

    [HttpGet]
    public async Task<IActionResult> MilestoneDocument(int milestoneId, CancellationToken ct)
    {
        var m = await sales.GetMilestoneAsync(milestoneId, ct);
        if (m?.DocumentStorageKey is null) return NotFound();
        var stream = await storage.OpenReadAsync(m.DocumentStorageKey, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(m.DocumentStorageKey), m.DocumentFileName);
    }

    // ---- Cancel ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Cancel(int id, string reason, bool makeUnitInactive, CancellationToken ct)
        => Done(await sales.CancelAsync(id, reason, makeUnitInactive, ct), id, "Sale cancelled. The unit is free again.");

    // ---- PDF and reports ----

    [HttpGet]
    public async Task<IActionResult> Statement(int id, CancellationToken ct)
    {
        var d = await sales.GetAsync(id, ct);
        if (d is null) return NotFound();
        var number = d.Sale.SaleNumber;
        var charge = await credits.TryConsumeAsync(CreditCosts.StatementPdf, $"Buyer statement PDF {number}", number, ct);
        if (!charge.Succeeded) { TempData["Error"] = charge.Error; return RedirectToAction(nameof(Details), new { id }); }
        return File(pdf.SaleStatement(d, await branding.ForPdfAsync(ct)), "application/pdf", $"{number}-statement.pdf");
    }

    [RequireFinancials]
    public async Task<IActionResult> Reports(CancellationToken ct)
        => View(new SalesReportsViewModel
        {
            Totals = await sales.GetTotalsAsync(ct),
            Inventory = await sales.GetInventoryAsync(ct),
            Arrears = await sales.GetInstalmentArrearsAsync(ct),
            Pipeline = await sales.ListAsync(SalesListView.Reserved, null, null, null, ct),
            Sold = (await sales.ListAsync(SalesListView.All, null, null, null, ct)).Where(s => s.Status is SaleStatus.Active or SaleStatus.Completed).ToList()
        });

    private IActionResult Done(Result result, int id, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillAsync(SaleForm form, CancellationToken ct)
    {
        form.Buyers = await sales.ListBuyersAsync(null, ct);
        form.Units = await sales.GetSellableUnitsAsync(ct);
        var scope = await agentScope.GetAsync(ct);
        form.ShowAgent = currentUser.BusinessType == BusinessType.Agency && !scope.IsRestricted;
        form.Agents = form.ShowAgent ? await agency.ListAgentsAsync(false, ct) : [];
    }
}
