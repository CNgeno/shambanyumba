using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[RequireCapability(Capability.Agency)]
public class ContractsController(IAgencyService agency, IAgentScopeService agentScope, ICurrentUserService currentUser, TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(ContractStatus? status, CancellationToken ct)
    {
        var isAgent = (await agentScope.GetAsync(ct)).IsRestricted;
        return View(new ContractsIndexViewModel { Contracts = await agency.ListContractsAsync(null, status, ct), Status = status, CanEdit = CanEdit && !isAgent, IsAgent = isAgent });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var d = await agency.GetContractAsync(id, ct);
        if (d is null) return NotFound();
        var isAgent = (await agentScope.GetAsync(ct)).IsRestricted;
        return View(new ContractDetailsViewModel
        {
            Details = d, Available = await agency.GetAvailablePropertiesAsync(ct), CanEdit = CanEdit && !isAgent, CanPrepare = CanEdit, Today = clock.TodayEat()
        });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(int? ownerId, CancellationToken ct)
    {
        var form = new ContractForm { OwnerId = ownerId, StartDate = clock.TodayEat().ToDateTime(TimeOnly.MinValue) };
        await FillAsync(form, ct);
        return View("Form", form);
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var d = await agency.GetContractAsync(id, ct);
        if (d is null) return NotFound();
        var c = d.Contract;
        var form = new ContractForm
        {
            Id = c.Id, ContractNumber = c.ContractNumber, Status = c.Status, OwnerId = c.OwnerId, AgentId = c.AgentId,
            StartDate = c.StartDate.ToDateTime(TimeOnly.MinValue), EndDate = c.EndDate?.ToDateTime(TimeOnly.MinValue),
            CommissionType = c.CommissionType, CommissionPercent = c.CommissionPercent, FixedMonthlyFee = c.FixedMonthlyFee,
            RemittanceDay = c.RemittanceDay, Notes = c.Notes
        };
        await FillAsync(form, ct);
        return View("Form", form);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Save(ContractForm form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            Result<int> result;
            if (form.Id is int id)
            {
                var updated = await agency.UpdateContractAsync(id, form.ToInput(), ct);
                result = updated.Succeeded ? Result<int>.Ok(id) : Result<int>.Fail(updated.Error!);
            }
            else
            {
                result = await agency.CreateContractAsync(form.ToInput(), ct);
            }
            if (result.Succeeded)
            {
                TempData["Success"] = form.Id is null ? "Contract created as a draft. Add its properties, then activate it." : "Contract saved.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        await FillAsync(form, ct);
        return View("Form", form);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddProperty(int id, int assetId, CancellationToken ct) => Done(await agency.AddPropertyAsync(id, assetId, ct), id, "Property added to the contract.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> RemoveProperty(int id, int linkId, CancellationToken ct) => Done(await agency.RemovePropertyAsync(id, linkId, ct), id, "Property removed from the contract.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct) => Done(await agency.ActivateAsync(id, ct), id, "Contract is now active.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> End(int id, DateTime endedOn, CancellationToken ct)
        => Done(await agency.EndAsync(id, DateOnly.FromDateTime(endedOn), ct), id, "Contract ended. Its properties can go under a new contract.");

    private IActionResult Done(Result result, int id, string success)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? success : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task FillAsync(ContractForm form, CancellationToken ct)
    {
        form.Owners = await agency.ListOwnersAsync(null, ct);
        form.Agents = await agency.ListAgentsAsync(false, ct);
    }
}
