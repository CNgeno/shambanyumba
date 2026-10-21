using System.Security.Claims;
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

public class PaymentsController(
    IPaymentService payments,
    ITenantService tenants,
    ILeaseService leases,
    ICreditService credits,
    IPdfService pdf,
    BusinessBranding branding,
    ITenantMessagingService messaging,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);
    private bool CanAdmin => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    public async Task<IActionResult> Index(string? month, PaymentMethod? method, string? q, bool reversed = false, bool refunds = false, CancellationToken ct = default)
    {
        DateOnly? monthDate = DateOnly.TryParseExact((month ?? "") + "-01", "yyyy-MM-dd", out var m) ? m : null;
        return View(new PaymentsIndexViewModel
        {
            Payments = refunds ? [] : await payments.ListAsync(new PaymentListFilter(monthDate, method, q, reversed), ct),
            Refunds = refunds ? await payments.ListRefundsAsync(monthDate, ct) : [],
            Summary = await payments.GetSummaryAsync(ct),
            ShowRefunds = refunds,
            Month = month,
            Method = method,
            Search = q,
            IncludeReversed = reversed,
            CanEdit = CanEdit
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await payments.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();
        return View(new PaymentDetailsViewModel
        {
            Details = details,
            BusinessName = User.FindFirstValue(AppClaimTypes.BusinessName) ?? "",
            CanEdit = CanEdit,
            CanAdmin = CanAdmin
        });
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var d = await payments.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        var number = d.Payment.ReceiptNumber;
        var charge = await credits.TryConsumeAsync(CreditCosts.ReceiptPdf, $"Receipt PDF {number}", number, ct);
        if (!charge.Succeeded) { TempData["Error"] = charge.Error; return RedirectToAction(nameof(Details), new { id }); }

        var profile = await branding.ForPdfAsync(ct);
        return File(pdf.Receipt(d, profile), "application/pdf", $"{number}.pdf");
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> EmailReceipt(int id, CancellationToken ct)
    {
        var result = await messaging.SendReceiptAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Receipt emailed to the tenant." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Record ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Record(int? tenantId, int? invoiceId, CancellationToken ct)
    {
        var model = new RecordPaymentViewModel { TenantId = tenantId, PaymentDate = clock.TodayEat().ToDateTime(TimeOnly.MinValue) };
        await FillAsync(model, ct);

        // Coming from an invoice: pay that invoice's balance against it.
        if (invoiceId is int iid && model.OpenInvoices.FirstOrDefault(i => i.Id == iid) is { } inv)
        {
            model.Mode = AllocationMode.Manual;
            model.Amount = inv.Balance;
            model.Allocations[iid] = inv.Balance;
        }
        else if (model.Balance is { Outstanding: > 0 } b)
        {
            model.Amount = b.Outstanding;
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Record(RecordPaymentViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await payments.RecordAsync(model.ToInput(), ct);
            if (result.Succeeded)
            {
                var emailNote = await messaging.AutoSendReceiptAsync(result.Value, ct);
                TempData["Success"] = "Payment recorded. Here's the receipt." + (emailNote is null ? "" : " " + emailNote);
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await FillAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> ApplyCredit(int tenantId, string? returnUrl, CancellationToken ct)
    {
        var result = await payments.ApplyCreditAsync(tenantId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"KES {result.Value:N2} of credit applied to unpaid invoices." : result.Error;
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Details", "Tenants", new { id = tenantId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Reverse(int id, string reason, CancellationToken ct)
    {
        var result = await payments.ReverseAsync(id, reason, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "Payment reversed. The invoices it paid are owed again." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---- Refunds ----

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> RefundOverpayment(int paymentId, CancellationToken ct)
    {
        var d = await payments.GetDetailsAsync(paymentId, ct);
        if (d is null) return NotFound();
        return View("Refund", new RefundViewModel
        {
            Kind = RefundKind.Overpayment, PaymentId = paymentId, TenantName = d.Payment.Tenant.FullName,
            Context = $"Unallocated balance of receipt {d.Payment.ReceiptNumber}", Available = d.Unallocated, Amount = d.Unallocated,
            RefundDate = clock.TodayEat().ToDateTime(TimeOnly.MinValue)
        });
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> RefundDeposit(int leaseId, CancellationToken ct)
    {
        var d = await leases.GetDetailsAsync(leaseId, ct);
        if (d is null) return NotFound();
        var available = await payments.GetDepositRefundableAsync(leaseId, ct);
        return View("Refund", new RefundViewModel
        {
            Kind = RefundKind.Deposit, LeaseId = leaseId, TenantName = d.Lease.Tenant.FullName,
            Context = $"Deposit on lease {d.Lease.LeaseNumber}: {d.Lease.DepositAmount:N2} held, {(d.Lease.DepositDeductions ?? 0):N2} deducted",
            Available = available, Amount = available, RefundDate = clock.TodayEat().ToDateTime(TimeOnly.MinValue)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Refund(RefundViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = model.Kind == RefundKind.Deposit
                ? await payments.RefundDepositAsync(model.LeaseId!.Value, model.ToInput(), ct)
                : await payments.RefundOverpaymentAsync(model.PaymentId!.Value, model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = $"{model.Kind.Display()} of KES {model.Amount:N2} recorded.";
                return model.Kind == RefundKind.Deposit
                    ? RedirectToAction("Details", "Leases", new { id = model.LeaseId })
                    : RedirectToAction(nameof(Details), new { id = model.PaymentId });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        return View("Refund", model);
    }

    // ---------------------------------------------------------------------

    private async Task FillAsync(RecordPaymentViewModel model, CancellationToken ct)
    {
        model.TenantOptions = await tenants.GetOptionsAsync(ct);
        if (model.TenantId is not int tenantId) return;

        model.TenantName = model.TenantOptions.FirstOrDefault(t => t.Id == tenantId)?.FullName;
        model.OpenInvoices = await payments.GetOpenInvoicesAsync(tenantId, ct);
        model.Balance = await payments.GetTenantBalanceAsync(tenantId, ct);
        model.LeaseOptions = (await tenants.GetDetailsAsync(tenantId, ct))?.Leases.Where(l => l.Status == LeaseStatus.Active).ToList() ?? [];
        if (model.LeaseId is null && model.LeaseOptions.Count == 1) model.LeaseId = model.LeaseOptions[0].Id;
    }
}
