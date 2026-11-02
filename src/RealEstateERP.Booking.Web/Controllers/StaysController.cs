using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Booking.Web.Models;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>Host: which units are short stays, their details, rules, rates and seasons, with a price preview.</summary>
[Authorize, Route("host/stays")]
public class StaysController(IStayService stays, ICurrentUserService currentUser) : Controller
{
    private bool CanEdit => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin or BusinessRole.User;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (currentUser.BusinessId is null) return Redirect("/host");
        ViewData["CanEdit"] = CanEdit;
        return View(await stays.ListAsync(ct));
    }

    [HttpPost("enable/{unitId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(int unitId, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var result = await stays.EnableAsync(unitId, ct);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }
        TempData["Success"] = "It's a short stay now. Add the details and rates, then publish it.";
        return RedirectToAction(nameof(Edit), new { id = result.Value });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, DateOnly? checkIn, DateOnly? checkOut, CancellationToken ct)
    {
        var stay = await stays.GetAsync(id, ct);
        if (stay is null) return NotFound();
        ViewData["Stay"] = stay;
        ViewData["CanEdit"] = CanEdit;
        if (checkIn is DateOnly ci && checkOut is DateOnly co) ViewData["Quote"] = await stays.QuoteAsync(id, ci, co, ct);
        return View(new StayEditModel
        {
            Title = stay.Title, Description = stay.Description, MaxGuests = stay.MaxGuests, Beds = stay.Beds, Bathrooms = stay.Bathrooms,
            Amenities = stay.Amenities, HouseRules = stay.HouseRules, CheckInFrom = stay.CheckInFrom, CheckOutBy = stay.CheckOutBy,
            MinNights = stay.MinNights, MaxAdvanceDays = stay.MaxAdvanceDays, CancellationPolicy = stay.CancellationPolicy,
            DepositPercent = stay.DepositPercent, BaseNightly = stay.BaseNightly, WeekendNightly = stay.WeekendNightly,
            CleaningFee = stay.CleaningFee, MaxNights = stay.MaxNights, IsPublished = stay.IsPublished
        });
    }

    [HttpPost("{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StayEditModel model, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var stay = await stays.GetAsync(id, ct);
        if (stay is null) return NotFound();
        if (ModelState.IsValid)
        {
            var result = await stays.SaveAsync(id, model.ToInput(), ct);
            if (result.Succeeded) { TempData["Success"] = "Saved."; return RedirectToAction(nameof(Edit), new { id }); }
            ModelState.AddModelError("", result.Error!);
        }
        ViewData["Stay"] = stay;
        ViewData["CanEdit"] = CanEdit;
        return View(model);
    }

    [HttpPost("{id:int}/seasons"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSeason(int id, string name, DateOnly from, DateOnly to, decimal nightly, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var result = await stays.AddSeasonAsync(id, new StaySeasonInput(name ?? "", from, to, nightly), ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Season added." : result.Error;
        return Redirect($"/host/stays/{id}#seasons");
    }

    [HttpPost("{id:int}/discounts"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDiscount(int id, int minNights, int percent, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var result = await stays.AddDiscountAsync(id, minNights, percent, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Discount added." : result.Error;
        return Redirect($"/host/stays/{id}#discounts");
    }

    [HttpPost("{id:int}/discounts/{discountId:int}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDiscount(int id, int discountId, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var result = await stays.RemoveDiscountAsync(id, discountId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Discount removed." : result.Error;
        return Redirect($"/host/stays/{id}#discounts");
    }

    [HttpPost("{id:int}/seasons/{seasonId:int}/remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSeason(int id, int seasonId, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var result = await stays.RemoveSeasonAsync(id, seasonId, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "Season removed." : result.Error;
        return Redirect($"/host/stays/{id}#seasons");
    }
}
