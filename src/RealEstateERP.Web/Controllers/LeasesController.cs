using RealEstateERP.Web.Formatting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

public class LeasesController(
    ILeaseService leases,
    ITenantService tenants,
    IAssetService assets,
    ISystemConfigService config,
    IChargeService charges,
    IInvoiceService invoices,
    IPaymentService payments,
    ICurrentUserService currentUser,
    TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(LeaseListView view = LeaseListView.Active, int? assetId = null, string? q = null, CancellationToken ct = default)
        => View(new LeasesIndexViewModel
        {
            Leases = await leases.ListAsync(new LeaseListFilter(view, assetId, q), ct),
            Assets = (await assets.ListAsync(ct)).ToLet(),
            Counts = await leases.GetCountsAsync(ct),
            View = view,
            AssetId = assetId,
            Search = q,
            CanEdit = CanEdit
        });

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await leases.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();
        var lease = details.Lease;
        var leaseInvoices = await invoices.ListForLeaseAsync(id, ct);
        return View(new LeaseDetailsViewModel
        {
            Details = details,
            DepositRefundable = await payments.GetDepositRefundableAsync(id, ct),
            Billing = new LeaseBillingViewModel
            {
                LeaseId = id,
                CanEdit = CanEdit,
                CanAddCharges = lease.Status is LeaseStatus.Draft or LeaseStatus.Active,
                Charges = await charges.GetLeaseChargesAsync(id, ct),
                AvailableCharges = await charges.ListDefinitionsAsync(false, ct),
                Invoices = leaseInvoices,
                NeedsFinalInvoice = lease.Status == LeaseStatus.Terminated && lease.TerminationPenalty > 0
                    && leaseInvoices.All(i => i.Kind != InvoiceKind.Final || i.Status == InvoiceStatus.Cancelled)
            },
            Today = clock.TodayEat(),
            CanEdit = CanEdit,
            MaxUploadMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct)
        });
    }

    // ---- Create / edit draft ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int? unitId, int? tenantId, CancellationToken ct)
    {
        var today = clock.TodayEat();
        var start = today.Day == 1 ? today : new DateOnly(today.Year, today.Month, 1).AddMonths(1);
        var model = new LeaseFormViewModel
        {
            UnitId = unitId,
            TenantId = tenantId,
            StartDate = start.ToDateTime(TimeOnly.MinValue),
            EndDate = start.AddYears(1).AddDays(-1).ToDateTime(TimeOnly.MinValue)
        };
        await FillAsync(model, null, ct);

        if (unitId is int uid && model.UnitOptions.FirstOrDefault(u => u.Id == uid) is { } unit)
        {
            model.MonthlyRent = unit.MonthlyRent;
            model.DepositAmount = unit.MonthlyRent;
        }
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(LeaseFormViewModel model, string? submit, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await FillAsync(model, null, ct);
            return View("Form", model);
        }

        var activate = submit == "activate";
        var result = await leases.CreateAsync(model.ToInput(), activate, ct);
        if (!result.Succeeded && result.Value == 0)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await FillAsync(model, null, ct);
            return View("Form", model);
        }

        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;   // saved as draft, activation failed
            return RedirectToAction(nameof(Details), new { id = result.Value });
        }

        TempData["Success"] = activate ? "Lease activated. The unit is now occupied." : "Draft lease saved.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await leases.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        if (d.Lease.Status != LeaseStatus.Draft)
        {
            TempData["Error"] = "Only draft leases can be edited. To change the terms of an active lease, renew it.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var l = d.Lease;
        var model = new LeaseFormViewModel
        {
            Id = l.Id, LeaseNumber = l.LeaseNumber, TenantId = l.TenantId, UnitId = l.UnitId,
            StartDate = l.StartDate.ToDateTime(TimeOnly.MinValue), EndDate = l.EndDate?.ToDateTime(TimeOnly.MinValue),
            MonthlyRent = l.MonthlyRent, DepositAmount = l.DepositAmount, NoticePeriodDays = l.NoticePeriodDays,
            RentDueDay = l.RentDueDay, Notes = l.Notes
        };
        await FillAsync(model, l.UnitId, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, LeaseFormViewModel model, string? submit, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await FillAsync(model, model.UnitId, ct);
            return View("Form", model);
        }

        var result = await leases.UpdateDraftAsync(id, model.ToInput(), ct);
        if (result.Succeeded && submit == "activate")
            result = await leases.ActivateAsync(id, ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await FillAsync(model, model.UnitId, ct);
            return View("Form", model);
        }

        TempData["Success"] = submit == "activate" ? "Lease activated. The unit is now occupied." : "Draft saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var result = await leases.ActivateAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Lease activated. The unit is now occupied." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await leases.DeleteDraftAsync(id, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = "Draft lease deleted.";
        return RedirectToAction(nameof(Index), new { view = LeaseListView.Draft });
    }

    // ---- Renew ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Renew(int id, CancellationToken ct)
    {
        var d = await leases.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        var l = d.Lease;
        var start = (l.EndDate ?? clock.TodayEat()).AddDays(1);

        return View(new LeaseRenewViewModel
        {
            LeaseId = id,
            Current = ToSummary(d),
            StartDate = start.ToDateTime(TimeOnly.MinValue),
            EndDate = start.AddYears(1).AddDays(-1).ToDateTime(TimeOnly.MinValue),
            MonthlyRent = l.MonthlyRent,
            DepositAmount = l.DepositAmount,
            NoticePeriodDays = l.NoticePeriodDays,
            RentDueDay = l.RentDueDay
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Renew(int id, LeaseRenewViewModel model, CancellationToken ct)
    {
        model.LeaseId = id;
        if (ModelState.IsValid)
        {
            var result = await leases.RenewAsync(id, model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = "Lease renewed. The previous lease is kept in the history.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        var d = await leases.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        model.Current = ToSummary(d);
        return View(model);
    }

    // ---- Terminate / end ----

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Terminate(int id, CancellationToken ct)
    {
        var d = await leases.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        return View(new LeaseTerminateViewModel
        {
            LeaseId = id,
            Current = ToSummary(d),
            DepositHeld = d.Lease.DepositAmount,
            NoticePeriodDays = d.Lease.NoticePeriodDays,
            EndDate = clock.TodayEat().ToDateTime(TimeOnly.MinValue)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Terminate(int id, LeaseTerminateViewModel model, CancellationToken ct)
    {
        model.LeaseId = id;
        if (ModelState.IsValid)
        {
            var result = await leases.TerminateAsync(id, model.ToInput(), ct);
            if (result.Succeeded)
            {
                TempData["Success"] = model.MakeUnitInactive ? "Lease ended. The unit is now inactive." : "Lease ended. The unit is available again.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }

        var d = await leases.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        model.Current = ToSummary(d);
        model.DepositHeld = d.Lease.DepositAmount;
        model.NoticePeriodDays = d.Lease.NoticePeriodDays;
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> EndAtExpiry(int id, bool makeUnitInactive, CancellationToken ct)
    {
        var result = await leases.EndAtExpiryAsync(id, makeUnitInactive, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Lease marked as expired. The unit is free again." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------------------------------------------------------------------

    private async Task FillAsync(LeaseFormViewModel model, int? currentUnitId, CancellationToken ct)
    {
        model.TenantOptions = await tenants.GetOptionsAsync(ct);
        model.UnitOptions = await leases.GetLettableUnitsAsync(currentUnitId ?? model.UnitId, ct);
    }

    private LeaseSummary ToSummary(LeaseDetails d)
    {
        var l = d.Lease;
        return new LeaseSummary(l.Id, l.LeaseNumber, l.TenantId, l.Tenant.FullName, l.UnitId, l.Unit.UnitNumber,
            l.Unit.AssetId, l.Unit.Asset.Name, l.StartDate, l.EndDate, l.MonthlyRent, l.Status, d.Expiry);
    }
}
