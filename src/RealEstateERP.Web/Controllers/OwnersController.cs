using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[RequireCapability(Capability.Agency)]
public class OwnersController(IAgencyService agency, IRemittanceService remittances, IAgentScopeService agentScope, ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);
    private async Task<bool> IsAgentAsync(CancellationToken ct) => (await agentScope.GetAsync(ct)).IsRestricted;

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var isAgent = await IsAgentAsync(ct);
        return View(new OwnersIndexViewModel { Owners = await agency.ListOwnersAsync(q, ct), Search = q, CanEdit = CanEdit && !isAgent, IsAgent = isAgent });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var owner = await agency.GetOwnerAsync(id, ct);
        if (owner is null) return NotFound();
        return View(new OwnerDetailsViewModel
        {
            Owner = owner,
            Contracts = await agency.ListContractsAsync(id, null, ct),
            Remittances = await remittances.ListAsync(null, null, id, ct),
            CanEdit = CanEdit && !await IsAgentAsync(ct)
        });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public IActionResult Create() => View("Form", new OwnerForm());

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var o = await agency.GetOwnerAsync(id, ct);
        if (o is null) return NotFound();
        return View("Form", new OwnerForm
        {
            Id = o.Id, Type = o.Type, FullName = o.FullName, IdNumber = o.IdNumber, KraPin = o.KraPin,
            Phone = Core.Common.KenyanPhone.Display(o.Phone), Email = o.Email, PostalAddress = o.PostalAddress, PayoutMethod = o.PayoutMethod,
            BankName = o.BankName, BankAccountName = o.BankAccountName, BankAccountNumber = o.BankAccountNumber,
            MpesaNumber = Core.Common.KenyanPhone.Display(o.MpesaNumber), Notes = o.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Save(OwnerForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", form);
        var result = await agency.SaveOwnerAsync(form.Id, form.ToInput(), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View("Form", form); }
        TempData["Success"] = $"{form.FullName} saved.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }
}
