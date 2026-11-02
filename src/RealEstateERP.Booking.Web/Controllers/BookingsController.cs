using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>Host: calendar, bookings, blocks, check-in and check-out, cleaning.</summary>
[Authorize, Route("host")]
public class BookingsController(IBookingService bookings, IStayService stays, ICurrentUserService currentUser, TimeProvider clock) : Controller
{
    private bool CanEdit => currentUser.Role is BusinessRole.Owner or BusinessRole.Admin or BusinessRole.User;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));

    [HttpGet("calendar")]
    public async Task<IActionResult> Calendar(DateOnly? from, CancellationToken ct)
    {
        if (currentUser.BusinessId is null) return Redirect("/host");
        ViewData["CanEdit"] = CanEdit;
        ViewData["Today"] = Today;
        return View(await bookings.CalendarAsync(from ?? Today, 14, ct));
    }

    [HttpPost("calendar/block"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Block(int stayUnitId, DateOnly from, DateOnly to, string? reason, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await bookings.BlockAsync(stayUnitId, from, to, reason ?? "", ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Dates blocked." : r.Error;
        return Redirect($"/host/calendar?from={from:yyyy-MM-dd}");
    }

    [HttpPost("calendar/unblock/{blockId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Unblock(int blockId, DateOnly? from, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await bookings.UnblockAsync(blockId, ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Dates opened again." : r.Error;
        return Redirect($"/host/calendar{(from is DateOnly f ? $"?from={f:yyyy-MM-dd}" : "")}");
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> Index(string? view, CancellationToken ct)
    {
        if (currentUser.BusinessId is null) return Redirect("/host");
        ViewData["View"] = view ?? "upcoming";
        return View(await bookings.ListAsync(view ?? "upcoming", ct));
    }

    [HttpGet("bookings/new")]
    public async Task<IActionResult> New(int? stayUnitId, DateOnly? checkIn, DateOnly? checkOut, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        ViewData["Stays"] = (await stays.ListAsync(ct)).Where(s => s.StayUnitId is not null).ToList();
        var ci = checkIn ?? Today;
        return View(new NewBookingInput(stayUnitId ?? 0, ci, checkOut ?? ci.AddDays(1), 1, 0, "", "", null, BookingChannel.WalkIn, null, null));
    }

    [HttpPost("bookings/new"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int stayUnitId, DateOnly checkIn, DateOnly checkOut, int adults, int children, string guestName,
        string guestPhone, string? guestEmail, BookingChannel channel, string? externalRef, string? notes, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var input = new NewBookingInput(stayUnitId, checkIn, checkOut, adults, children, guestName ?? "", guestPhone ?? "", guestEmail, channel, externalRef, notes);
        var r = await bookings.CreateAsync(input, ct);
        if (r.Succeeded) { TempData["Success"] = "Booking confirmed."; return Redirect($"/host/bookings/{r.Value}"); }
        TempData["Error"] = r.Error;
        ViewData["Stays"] = (await stays.ListAsync(ct)).Where(s => s.StayUnitId is not null).ToList();
        return View("New", input);
    }

    [HttpGet("bookings/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var b = await bookings.GetAsync(id, ct);
        if (b is null) return NotFound();
        ViewData["CanEdit"] = CanEdit;
        ViewData["Today"] = Today;
        return View(b);
    }

    [HttpPost("bookings/{id:int}/checkin"), ValidateAntiForgeryToken]
    public Task<IActionResult> CheckIn(int id, CancellationToken ct) => Act(id, () => bookings.CheckInAsync(id, ct), "Guest checked in.");

    [HttpPost("bookings/{id:int}/checkout"), ValidateAntiForgeryToken]
    public Task<IActionResult> CheckOut(int id, CancellationToken ct) => Act(id, () => bookings.CheckOutAsync(id, ct), "Guest checked out. A cleaning task was added.");

    [HttpPost("bookings/{id:int}/cancel"), ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(int id, string? reason, bool noShow, CancellationToken ct)
        => Act(id, () => bookings.CancelAsync(id, reason ?? "", noShow, ct), noShow ? "Marked as a no-show. The nights are free again." : "Booking cancelled. The nights are free again.");

    [HttpPost("bookings/{id:int}/payments"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(int id, StayPaymentMethod method, decimal amount, DateOnly receivedOn, string? reference, CancellationToken ct)
    {
        if (!CanEdit) return Forbid();
        var r = await bookings.RecordPaymentAsync(id, method, amount, receivedOn, reference, ct);
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Payment recorded. A receipt was emailed if the guest has an email." : r.Error;
        return Redirect($"/host/bookings/{id}#payments");
    }

    [HttpPost("bookings/{id:int}/confirmation"), ValidateAntiForgeryToken]
    public Task<IActionResult> SendConfirmation(int id, CancellationToken ct)
        => Act(id, () => bookings.SendConfirmationAsync(id, ct), "Confirmation emailed to the guest, with how to pay.");

    [HttpGet("cleaning")]
    public async Task<IActionResult> Cleaning(bool all, CancellationToken ct)
    {
        if (currentUser.BusinessId is null) return Redirect("/host");
        ViewData["All"] = all;
        return View(await bookings.CleaningAsync(all, ct));
    }

    [HttpPost("cleaning/{taskId:int}/done"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CleaningDone(int taskId, CancellationToken ct)
    {
        var r = await bookings.CompleteCleaningAsync(taskId, ct);   // anyone in the team, including cleaners
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? "Marked clean and ready." : r.Error;
        return Redirect("/host/cleaning");
    }

    private async Task<IActionResult> Act(int id, Func<Task<RealEstateERP.Core.Common.Result>> action, string ok)
    {
        if (!CanEdit) return Forbid();
        var r = await action();
        TempData[r.Succeeded ? "Success" : "Error"] = r.Succeeded ? ok : r.Error;
        return Redirect($"/host/bookings/{id}");
    }
}
