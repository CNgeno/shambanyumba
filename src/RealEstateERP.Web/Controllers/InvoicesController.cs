using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

public class InvoicesController(
    IInvoiceService invoices,
    IBillingRunService billing,
    IChargeService charges,
    ITenantService tenants,
    ILeaseService leases,
    IAssetService assets,
    IPaymentService payments,
    ICreditService credits,
    IPdfService pdf,
    BusinessBranding branding,
    ITenantMessagingService messaging,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);
    private bool CanAdmin => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    public async Task<IActionResult> Index(InvoiceListView view = InvoiceListView.Unpaid, string? month = null, int? assetId = null, string? q = null, CancellationToken ct = default)
    {
        DateOnly? monthDate = DateOnly.TryParseExact((month ?? "") + "-01", "yyyy-MM-dd", out var m) ? m : null;
        return View(new InvoicesIndexViewModel
        {
            Invoices = await invoices.ListAsync(new InvoiceListFilter(view, monthDate, assetId, q), ct),
            Assets = await assets.ListAsync(ct),
            Summary = await invoices.GetSummaryAsync(ct),
            View = view,
            Month = month,
            AssetId = assetId,
            Search = q,
            CanEdit = CanEdit
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var d = await invoices.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        return View(new InvoiceDetailsViewModel
        {
            Details = d,
            Payments = await payments.ListForInvoiceAsync(id, ct),
            CanEdit = CanEdit,
            CanAdmin = CanAdmin,
            BusinessName = User.FindFirstValue(AppClaimTypes.BusinessName) ?? ""
        });
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var d = await invoices.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        var number = d.Invoice.InvoiceNumber;
        var charge = await credits.TryConsumeAsync(CreditCosts.InvoicePdf, $"Invoice PDF {number}", number, ct);
        if (!charge.Succeeded) { TempData["Error"] = charge.Error; return RedirectToAction(nameof(Details), new { id }); }

        var profile = await branding.ForPdfAsync(ct);
        return File(pdf.Invoice(d, profile), "application/pdf", $"{number}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Email(int id, CancellationToken ct)
    {
        var result = await messaging.SendInvoiceAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Invoice emailed to the tenant." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Monthly generation ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Generate(string? month, int? assetId, CancellationToken ct)
    {
        var model = new GenerateInvoicesViewModel { Month = month ?? clock.TodayEat().ToString("yyyy-MM"), AssetId = assetId };
        await FillGenerateAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Generate(GenerateInvoicesViewModel model, CancellationToken ct)
    {
        var (year, monthNumber) = model.Parse();
        if (year == 0)
        {
            TempData["Error"] = "Choose the month to bill.";
            return RedirectToAction(nameof(Generate));
        }

        var result = await billing.GenerateAsync(new BillingRunRequest(year, monthNumber, model.AssetId, model.IncludeDeposits, model.IssueImmediately), ct);
        var summary = $"{result.Created} invoice(s) created for {new DateOnly(year, monthNumber, 1):MMMM yyyy}, totalling {result.TotalInvoiced:N2}."
                      + (result.Skipped > 0 ? $" {result.Skipped} lease(s) were already invoiced." : "");
        TempData[result.Created > 0 || result.Problems.Count == 0 ? "Success" : "Error"] = summary
            + (result.Problems.Count > 0 ? " " + string.Join(" ", result.Problems.Take(3)) : "");

        return RedirectToAction(nameof(Index), new { view = model.IssueImmediately ? InvoiceListView.Unpaid : InvoiceListView.Draft });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> IssueAll(CancellationToken ct)
    {
        var result = await invoices.IssueAllDraftsAsync(ct);
        TempData["Success"] = $"{result.Value} draft invoice(s) issued.";
        return RedirectToAction(nameof(Index), new { view = InvoiceListView.Unpaid });
    }

    // ---- Manual invoices ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int? tenantId, int? leaseId, CancellationToken ct)
    {
        var today = clock.TodayEat();
        var model = new InvoiceFormViewModel
        {
            TenantId = tenantId,
            LeaseId = leaseId,
            InvoiceDate = today.ToDateTime(TimeOnly.MinValue),
            DueDate = today.AddDays(7).ToDateTime(TimeOnly.MinValue),
            Lines = [new InvoiceLineForm()]
        };
        await FillFormAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(InvoiceFormViewModel model, string? submit, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await invoices.CreateManualAsync(model.ToInput(), issue: submit == "issue", ct);
            if (result.Succeeded)
            {
                TempData["Success"] = submit == "issue" ? "Invoice issued." : "Draft invoice saved.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await FillFormAsync(model, ct);
        return View("Form", model);
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await invoices.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        var i = d.Invoice;
        if (i.Status != InvoiceStatus.Draft || i.Kind != InvoiceKind.Manual)
        {
            TempData["Error"] = "Only manual draft invoices can be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = new InvoiceFormViewModel
        {
            Id = i.Id, InvoiceNumber = i.InvoiceNumber, TenantId = i.TenantId, LeaseId = i.LeaseId,
            InvoiceDate = i.InvoiceDate.ToDateTime(TimeOnly.MinValue), DueDate = i.DueDate.ToDateTime(TimeOnly.MinValue), Notes = i.Notes,
            Lines = d.Lines.Select(l => new InvoiceLineForm
            {
                LineType = l.LineType, Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice, TaxPercent = l.TaxPercent
            }).ToList()
        };
        await FillFormAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, InvoiceFormViewModel model, string? submit, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid)
        {
            var result = await invoices.UpdateDraftAsync(id, model.ToInput(), ct);
            if (result.Succeeded && submit == "issue") result = await invoices.IssueAsync(id, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = submit == "issue" ? "Invoice issued." : "Draft saved.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await FillFormAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Final(int leaseId, CancellationToken ct)
    {
        var result = await invoices.CreateFinalInvoiceAsync(leaseId, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Details", "Leases", new { id = leaseId });
        }
        TempData["Success"] = "Final invoice created as a draft. Review and issue it.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    // ---- Lifecycle ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Issue(int id, CancellationToken ct) => Done(await invoices.IssueAsync(id, ct), id, "Invoice issued.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await invoices.DeleteDraftAsync(id, ct);
        if (!result.Succeeded) return Done(result, id, "");
        TempData["Success"] = "Draft deleted. Any meter readings on it can be billed again.";
        return RedirectToAction(nameof(Index), new { view = InvoiceListView.Draft });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken ct)
        => Done(await invoices.CancelAsync(id, reason, ct), id, "Invoice cancelled. Its meter readings can be billed again.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> WriteOff(int id, string reason, CancellationToken ct)
        => Done(await invoices.WriteOffAsync(id, reason, ct), id, "Invoice written off.");

    // ---------------------------------------------------------------------

    private IActionResult Done(Result result, int id, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillGenerateAsync(GenerateInvoicesViewModel model, CancellationToken ct)
    {
        model.Assets = await assets.ListAsync(ct);
        var (year, month) = model.Parse();
        if (year > 0)
            model.Preview = await billing.PreviewAsync(new BillingRunRequest(year, month, model.AssetId, model.IncludeDeposits, false), ct);
        model.UnbilledReadings = (await charges.ListReadingsAsync(true, model.AssetId, ct)).Count;
    }

    private async Task FillFormAsync(InvoiceFormViewModel model, CancellationToken ct)
    {
        model.TenantOptions = await tenants.GetOptionsAsync(ct);
        model.Charges = await charges.ListDefinitionsAsync(false, ct);
        // All billable leases; the form shows only the chosen tenant's (filtered in the browser).
        model.LeaseOptions = (await leases.ListAsync(new LeaseListFilter(LeaseListView.All, null, null), ct))
            .Where(l => l.Status is LeaseStatus.Active or LeaseStatus.Terminated or LeaseStatus.Expired)
            .ToList();
        if (model.Lines.Count == 0) model.Lines.Add(new InvoiceLineForm());
    }
}
