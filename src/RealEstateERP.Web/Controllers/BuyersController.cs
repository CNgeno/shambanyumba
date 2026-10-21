using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[RequireCapability(Capability.Sales)]
public class BuyersController(ISaleService sales, ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is not (BusinessRole.Viewer or null);

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        ViewData["Search"] = q;
        ViewData["CanEdit"] = CanEdit;
        return View(await sales.ListBuyersAsync(q, ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var buyer = await sales.GetBuyerAsync(id, ct);
        if (buyer is null) return NotFound();
        return View(new BuyerDetailsViewModel { Buyer = buyer, Sales = await sales.ListAsync(SalesListView.All, id, null, null, ct), CanEdit = CanEdit });
    }

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public IActionResult Create(bool returnToSale = false) => View("Form", new BuyerForm { ReturnToSale = returnToSale });

    [HttpGet, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var b = await sales.GetBuyerAsync(id, ct);
        if (b is null) return NotFound();
        return View("Form", new BuyerForm
        {
            Id = b.Id, Type = b.Type, FullName = b.FullName, IdNumber = b.IdNumber, KraPin = b.KraPin,
            Phone = Core.Common.KenyanPhone.Display(b.Phone), Email = b.Email, PostalAddress = b.PostalAddress, Notes = b.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Save(BuyerForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", form);
        var result = await sales.SaveBuyerAsync(form.Id, form.ToInput(), ct);
        if (!result.Succeeded) { ModelState.AddModelError(string.Empty, result.Error!); return View("Form", form); }
        TempData["Success"] = $"{form.FullName} saved.";
        return form.ReturnToSale
            ? RedirectToAction("Create", "Sales", new { buyerId = result.Value })
            : RedirectToAction(nameof(Details), new { id = result.Value });
    }
}
