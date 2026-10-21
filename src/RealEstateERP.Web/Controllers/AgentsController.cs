using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[RequireCapability(Capability.Agency)]
public class AgentsController(IAgencyService agency, ICurrentUserService currentUser) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["CanAdmin"] = currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;
        return View(await agency.ListAgentsAsync(includeInactive: true, ct));
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Create(CancellationToken ct)
        => View("Form", new AgentForm { CommissionSharePercent = 30, Members = await agency.GetLinkableMembersAsync(ct) });

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Reassign(int id, int? toAgentId, CancellationToken ct)
    {
        var result = await agency.ReassignContractsAsync(id, toAgentId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? $"{result.Value} contract(s) moved{(toAgentId is null ? " and left without an agent" : "")}." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var a = await agency.GetAgentAsync(id, ct);
        if (a is null) return NotFound();
        return View("Form", new AgentForm
        {
            Id = a.Id, FullName = a.FullName, LicenseNumber = a.LicenseNumber, Phone = Core.Common.KenyanPhone.Display(a.Phone),
            Email = a.Email, CommissionSharePercent = a.CommissionSharePercent, IsActive = a.IsActive, UserId = a.UserId,
            Members = await agency.GetLinkableMembersAsync(ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Save(AgentForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var saved = await agency.SaveAgentAsync(form.Id, form.ToInput(), ct);
            if (saved.Succeeded)
            {
                TempData["Success"] = $"{form.FullName} saved.";
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, saved.Error!);
        }
        form.Members = await agency.GetLinkableMembersAsync(ct);
        return View("Form", form);
    }
}
