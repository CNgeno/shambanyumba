using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

public class PropertiesController(IAssetService assets, IUnitService units, IAgencyService agency, IProjectCostService costing, ISaleService sales, IEstateService estate, IChargeService charges, IReportService reports, ICapabilityService capabilities, IListingService listings, ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin or BusinessRole.User;
    private bool CanAdmin => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await assets.ListAsync(ct);
        return View(new PropertiesIndexViewModel
        {
            Assets = list,
            Totals = Core.Models.UnitStats.Combine(list.Select(a => a.Stats)),
            Usage = await units.GetUsageAsync(ct),
            CanEdit = CanEdit
        });
    }

    public async Task<IActionResult> Details(int id, UnitStatus? status, int? node, CancellationToken ct)
    {
        var details = await assets.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();

        var visible = details.Units
            .Where(u => status is null || u.Status == status)
            .Where(u => node is null || u.StructureNodeId == node)
            .ToList();

        if (currentUser.BusinessType == BusinessType.Agency
            && (await agency.GetManagedAssetsAsync(ct)).TryGetValue(id, out var managed))
            ViewData["ManagedFor"] = managed;

        // Land, or a developer's property with units for sale: show it as a project, not as a rental block.
        ViewData["Enquiries"] = await listings.GetPropertyEnquiriesAsync(id, ct);
        ViewData["AwaitingEnquiries"] = await listings.GetAwaitingConfirmationAsync(id, ct);

        // The property's purpose decides its page: for sale = a project (plots, buyers, margins); to let (or both) = an estate.
        var isProject = RealEstateERP.Core.Common.PropertyRules.Effective(details.Asset.Purpose, details.Asset.Type) == PropertyPurpose.Sell;
        if (isProject && await costing.GetOverviewAsync(id, ct) is { } overview)
        {
            var overdue = (await sales.ListAsync(SalesListView.Open, null, id, null, ct)).Sum(x => x.OverdueAmount);
            ViewData["Project"] = new LandProjectModel(ProjectCard.From(overview, overdue), overview, currentUser.Role is not (BusinessRole.Viewer or null));
        }
        else
        {
            // A rental estate: its guided setup, charges, photos and description.
            var seesFinances = await capabilities.CanViewFinancialsAsync(ct);
            ViewData["Estate"] = new EstatePanelModel(id, await estate.GetSetupAsync(id, ct), await estate.GetChargesAsync(id, ct),
                await charges.ListDefinitionsAsync(false, ct), await estate.GetEstatePhotosAsync(id, ct), details.Asset.Description,
                currentUser.Role is not (BusinessRole.Viewer or null),
                seesFinances ? (await reports.GetPropertyMoneyAsync(ct)).GetValueOrDefault(id) : null, seesFinances);
        }

        return View(new PropertyDetailsViewModel
        {
            Details = details,
            VisibleUnits = visible,
            Usage = await units.GetUsageAsync(ct),
            CanEdit = CanEdit,
            CanAdmin = CanAdmin,
            StatusFilter = status,
            NodeFilter = node
        });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public IActionResult Create() => View("Form", new AssetFormViewModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(AssetFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await assets.CreateAsync(model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        TempData["Success"] = $"{model.Name} added. Next, add its blocks and floors, or add units directly.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var details = await assets.GetDetailsAsync(id, ct);
        if (details is null) return NotFound();

        var a = details.Asset;
        return View("Form", new AssetFormViewModel
        {
            Id = a.Id, Name = a.Name, Type = a.Type, Purpose = RealEstateERP.Core.Common.PropertyRules.Effective(a.Purpose, a.Type), LifecycleState = a.LifecycleState, Location = a.Location,
            County = a.County, LandSizeAcres = a.LandSizeAcres, AcquisitionCost = a.AcquisitionCost,
            AcquisitionDate = a.AcquisitionDate?.ToDateTime(TimeOnly.MinValue), TitleDeedNumber = a.TitleDeedNumber,
            Description = a.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, AssetFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid) return View("Form", model);

        var result = await assets.UpdateAsync(id, model.ToInput(), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        TempData["Success"] = $"{model.Name} saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.BusinessAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await assets.DeleteAsync(id, ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }
        TempData["Success"] = "Property deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---- Structure (blocks, buildings, wings, floors) ----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> AddNode(int id, NodeFormInput input, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await assets.AddNodeAsync(id, input.ToInput(), ct)
            : Core.Common.Result<int>.Fail("Enter a name, for example Block A or Floor 1.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"{input.Name} added." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> UpdateNode(int id, int nodeId, NodeFormInput input, CancellationToken ct)
    {
        var result = ModelState.IsValid
            ? await assets.UpdateNodeAsync(nodeId, input.ToInput(), ct)
            : Core.Common.Result.Fail("Enter a name, for example Block A or Floor 1.");
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? $"{input.Name} saved." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> DeleteNode(int id, int nodeId, CancellationToken ct)
    {
        var result = await assets.DeleteNodeAsync(nodeId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Removed." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }
}
