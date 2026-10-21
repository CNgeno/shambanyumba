using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;
using RealEstateERP.Web.Services;

namespace RealEstateERP.Web.Controllers;

public class TenantsController(
    ITenantService tenants,
    IAssetService assets,
    IFileStorage storage,
    ISystemConfigService config,
    IPaymentService payments,
    IStatementService statements,
    IArrearsService arrears,
    ICreditService credits,
    IPdfService pdf,
    BusinessBranding branding,
    ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(TenantStatus? status, int? assetId, string? q, bool owing = false, CancellationToken ct = default)
    {
        var list = await tenants.ListAsync(new TenantListFilter(status, assetId, q), ct);
        var balances = await payments.GetTenantBalancesAsync(list.Select(t => t.Id).ToList(), ct);
        if (owing) list = list.Where(t => balances.TryGetValue(t.Id, out var b) && b.Outstanding > 0).ToList();

        return View(new TenantsIndexViewModel
        {
            Tenants = list,
            Balances = balances,
            Owing = owing,
            Assets = await assets.ListAsync(ct),
            Status = status,
            AssetId = assetId,
            Search = q,
            CanEdit = CanEdit
        });
    }

    public async Task<IActionResult> Statement(int id, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var statement = await statements.GetStatementAsync(id,
            from is DateTime f ? DateOnly.FromDateTime(f) : null, to is DateTime t ? DateOnly.FromDateTime(t) : null, ct);
        if (statement is null) return NotFound();
        return View(new TenantStatementViewModel
        {
            Statement = statement,
            Balance = await payments.GetTenantBalanceAsync(id, ct),
            BusinessName = User.FindFirstValue(Core.Constants.AppClaimTypes.BusinessName) ?? "",
            From = from,
            To = to
        });
    }

    [HttpGet]
    public async Task<IActionResult> StatementPdf(int id, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var statement = await statements.GetStatementAsync(id,
            from is DateTime f ? DateOnly.FromDateTime(f) : null, to is DateTime t ? DateOnly.FromDateTime(t) : null, ct);
        if (statement is null) return NotFound();
        var charge = await credits.TryConsumeAsync(Core.Constants.CreditCosts.StatementPdf, $"Statement PDF, {statement.Tenant.FullName}", null, ct);
        if (!charge.Succeeded) { TempData["Error"] = charge.Error; return RedirectToAction(nameof(Statement), new { id, from, to }); }

        var profile = await branding.ForPdfAsync(ct);
        var name = string.Concat(statement.Tenant.FullName.Where(char.IsLetterOrDigit));
        return File(pdf.Statement(statement, profile), "application/pdf", $"Statement-{name}-{statement.To:yyyyMMdd}.pdf");
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var details = await tenants.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();
        return View(new TenantDetailsViewModel
        {
            Details = details,
            Balance = await payments.GetTenantBalanceAsync(id, ct),
            Collections = new CollectionsPanelModel
            {
                TenantId = id,
                Stage = details.Tenant.CollectionStage,
                Notes = await arrears.GetNotesAsync(id, ct),
                Documents = details.Documents,
                CanEdit = CanEdit
            },
            RecentPayments = (await payments.ListAsync(new PaymentListFilter(null, null, null, true), ct))
                .Where(p => p.TenantId == id).Take(8).ToList(),
            CanEdit = CanEdit,
            MaxUploadMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct)
        });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public IActionResult Create(int? unitId) => View("Form", new TenantFormViewModel { ForUnitId = unitId });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(TenantFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await tenants.CreateAsync(model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        TempData["Success"] = $"{model.FullName} added.";
        return model.ForUnitId is int unitId
            ? RedirectToAction("Create", "Leases", new { unitId, tenantId = result.Value })
            : RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await tenants.GetDetailsAsync(id, ct);
        if (d is null) return NotFound();
        var t = d.Tenant;
        return View("Form", new TenantFormViewModel
        {
            Id = t.Id, Type = t.Type, FullName = t.FullName, IdType = t.IdType, IdNumber = t.IdNumber, KraPin = t.KraPin,
            Phone = Core.Common.KenyanPhone.Display(t.Phone), AltPhone = Core.Common.KenyanPhone.Display(t.AltPhone),
            Email = t.Email, PostalAddress = t.PostalAddress, Employer = t.Employer,
            EmergencyContactName = t.EmergencyContactName,
            EmergencyContactPhone = Core.Common.KenyanPhone.Display(t.EmergencyContactPhone),
            EmergencyContactRelationship = t.EmergencyContactRelationship, Notes = t.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, TenantFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await tenants.UpdateAsync(id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        TempData["Success"] = $"{model.FullName} saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await tenants.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = "Tenant deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---- Documents ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int id, DocumentUploadInput input, string? returnUrl, CancellationToken ct)
    {
        if (input.File is not { Length: > 0 } file)
        {
            TempData["Error"] = "Choose a file to upload.";
        }
        else
        {
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            var result = await tenants.UploadDocumentAsync(id, input.LeaseId, input.DocumentType, input.Title,
                new UploadedFile(buffer, file.FileName, file.Length), ct);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"{input.DocumentType.Display()} uploaded." : result.Error;
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, CancellationToken ct)
    {
        var doc = await tenants.GetDocumentAsync(id, ct);   // tenant-filtered: other businesses get 404
        if (doc is null) return NotFound();
        var stream = await storage.OpenReadAsync(doc.StorageKey, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(doc.FileName), doc.FileName);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> DeleteDocument(int id, int tenantId, string? returnUrl, CancellationToken ct)
    {
        var result = await tenants.DeleteDocumentAsync(id, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Document removed." : result.Error;
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Details), new { id = tenantId });
    }
}
