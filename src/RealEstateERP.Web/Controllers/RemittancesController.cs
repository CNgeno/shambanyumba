using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

[RequireCapability(Capability.Agency)]
public class RemittancesController(
    IRemittanceService remittances,
    IAgencyService agency,
    ISaleService sales,
    ICreditService credits,
    IPdfService pdf,
    BusinessBranding branding,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);
    private bool CanAdmin => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    /// <summary>Defaults to last month: owners are paid in arrears.</summary>
    public async Task<IActionResult> Index(string? month, RemittanceStatus? status, CancellationToken ct)
    {
        var m = ParseMonth(month) ?? clock.TodayEat().AddMonths(-1);
        return View(new RemittancesIndexViewModel
        {
            Remittances = await remittances.ListAsync(m, status, null, ct),
            ActiveContracts = await agency.ListContractsAsync(null, ContractStatus.Active, ct),
            Month = m.ToString("yyyy-MM"),
            Status = status,
            CanEdit = CanEdit
        });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Prepare(int contractId, string month, CancellationToken ct)
    {
        var m = ParseMonth(month);
        if (m is null) return BadRequest();
        var preview = await remittances.PreviewAsync(contractId, m.Value.Year, m.Value.Month, ct);
        return preview is null ? NotFound() : View(preview);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int contractId, string month, CancellationToken ct)
    {
        var m = ParseMonth(month);
        if (m is null) return BadRequest();
        var result = await remittances.CreateAsync(contractId, m.Value.Year, m.Value.Month, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index), new { month });
        }
        TempData["Success"] = "Draft statement created. Add any adjustments, then approve it.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var d = await remittances.GetAsync(id, ct);
        return d is null ? NotFound() : View(new RemittanceDetailsViewModel { Details = d, CanEdit = CanEdit, CanAdmin = CanAdmin });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddAdjustment(int id, string description, decimal amount, bool deduct, CancellationToken ct)
        => Done(await remittances.AddAdjustmentAsync(id, description, deduct ? -Math.Abs(amount) : Math.Abs(amount), ct), id, "Adjustment added.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemoveAdjustment(int id, int lineId, CancellationToken ct)
        => Done(await remittances.RemoveAdjustmentAsync(id, lineId, ct), id, "Adjustment removed.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Approve(int id, CancellationToken ct) => Done(await remittances.ApproveAsync(id, ct), id, "Statement approved. Pay the owner, then record the payment.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> MarkPaid(int id, DateTime paidOn, PayoutMethod method, string? reference, CancellationToken ct)
        => Done(await remittances.MarkPaidAsync(id, DateOnly.FromDateTime(paidOn), method, reference, ct), id, "Payment to the owner recorded.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken ct) => Done(await remittances.CancelAsync(id, reason, ct), id, "Statement cancelled. The month can be prepared again.");

    [HttpGet]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var d = await remittances.GetAsync(id, ct);
        if (d is null) return NotFound();
        var number = d.Remittance.RemittanceNumber;
        var charge = await credits.TryConsumeAsync(CreditCosts.StatementPdf, $"Owner statement PDF {number}", number, ct);
        if (!charge.Succeeded) { TempData["Error"] = charge.Error; return RedirectToAction(nameof(Details), new { id }); }
        return File(pdf.Remittance(d, await branding.ForPdfAsync(ct)), "application/pdf", $"{number}.pdf");
    }

    public async Task<IActionResult> AgentCommissions(string? month, CancellationToken ct)
    {
        var m = ParseMonth(month) ?? clock.TodayEat().AddMonths(-1);
        return View(new AgentCommissionsViewModel
        {
            Rows = await remittances.AgentCommissionsAsync(m.Year, m.Month, ct),
            SalesRows = await sales.SalesCommissionAsync(m.Year, m.Month, ct),
            Month = m.ToString("yyyy-MM")
        });
    }

    private IActionResult Done(Result result, int id, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private static DateOnly? ParseMonth(string? month)
        => DateOnly.TryParseExact((month ?? "") + "-01", "yyyy-MM-dd", out var d) ? d : null;
}
