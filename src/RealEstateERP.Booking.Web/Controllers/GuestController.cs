using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>The public guest site: search, stay pages, booking, "My booking", photos. No sign-in needed.</summary>
public class GuestController(IStaySearchService search, TimeProvider clock) : Controller
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));

    [HttpGet("/")]
    public async Task<IActionResult> Home(CancellationToken ct)
    {
        ViewData["Today"] = Today;
        return View(await search.SearchAsync(null, null, null, 1, ct));
    }

    [HttpGet("/stays")]
    public async Task<IActionResult> Search(string? where, DateOnly? checkIn, DateOnly? checkOut, int guests = 2, CancellationToken ct = default)
    {
        ViewData["Where"] = where; ViewData["CheckIn"] = checkIn; ViewData["CheckOut"] = checkOut; ViewData["Guests"] = guests; ViewData["Today"] = Today;
        return View(await search.SearchAsync(where, checkIn, checkOut, guests, ct));
    }

    [HttpGet("/stay/{id:int}")]
    public async Task<IActionResult> Stay(int id, DateOnly? checkIn, DateOnly? checkOut, int guests = 2, CancellationToken ct = default)
    {
        var stay = await search.GetAsync(id, checkIn, checkOut, ct);
        if (stay is null) return NotFound();
        ViewData["CheckIn"] = checkIn; ViewData["CheckOut"] = checkOut; ViewData["Guests"] = guests; ViewData["Today"] = Today;
        return View(stay);
    }

    [HttpPost("/stay/{id:int}/book"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int id, DateOnly checkIn, DateOnly checkOut, int adults, int children, string name, string phone,
        string? email, string? message, CancellationToken ct)
    {
        var result = await search.BookAsync(new PublicBookingInput(id, checkIn, checkOut, adults, children, name ?? "", phone ?? "", email, message), ct);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return Redirect($"/stay/{id}?checkIn={checkIn:yyyy-MM-dd}&checkOut={checkOut:yyyy-MM-dd}&guests={adults + children}#book");
        }
        return Redirect($"/my-booking?number={Uri.EscapeDataString(result.Value!)}&phone={Uri.EscapeDataString(phone ?? "")}&new=1");
    }

    [HttpGet("/my-booking")]
    public async Task<IActionResult> MyBooking(string? number, string? phone, bool @new = false, CancellationToken ct = default)
    {
        ViewData["Number"] = number; ViewData["Phone"] = phone; ViewData["New"] = @new;
        GuestBookingView? booking = null;
        if (!string.IsNullOrWhiteSpace(number) && !string.IsNullOrWhiteSpace(phone))
        {
            booking = await search.FindAsync(number, phone, ct);
            if (booking is null) ViewData["NotFound"] = true;
        }
        return View(booking);
    }

    [HttpGet("/photo/{kind}/{id:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Photo(string kind, int id, CancellationToken ct)
        => await search.OpenPhotoAsync(kind, id, ct) is { } photo ? File(photo.Content, photo.ContentType) : NotFound();
}
