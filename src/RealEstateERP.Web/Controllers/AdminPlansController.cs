using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[Authorize(Policy = Policies.SuperAdmin), AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("admin/plans")]
public class AdminPlansController(IPlanService plans) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await plans.GetAllAsync(ct));

    [HttpGet("new")]
    public IActionResult Create() => View("Edit", new PlanEditViewModel());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var p = await plans.GetAsync(id, ct);
        if (p is null) return NotFound();

        return View(new PlanEditViewModel
        {
            Id = p.Id, Code = p.Code, Name = p.Name, Description = p.Description, UnitLimit = p.UnitLimit,
            MonthlyFee = p.MonthlyFee, CreditsPerMonth = p.CreditsPerMonth, TrialDays = p.TrialDays,
            ForBusinessType = p.ForBusinessType, SortOrder = p.SortOrder, IsActive = p.IsActive
        });
    }

    [HttpPost("save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PlanEditViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        var result = await plans.SaveAsync(new SubscriptionPlanInput(
            model.Id, model.Code, model.Name, model.Description, model.UnitLimit, model.MonthlyFee,
            model.CreditsPerMonth, model.TrialDays, model.IsActive, model.SortOrder, model.ForBusinessType), ct);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Edit", model);
        }

        TempData["Success"] = $"{model.Name} plan saved. Pending payments keep the price they were submitted at.";
        return RedirectToAction(nameof(Index));
    }
}
