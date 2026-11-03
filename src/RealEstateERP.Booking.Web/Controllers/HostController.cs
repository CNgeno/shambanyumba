using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>The host area (booking.shambanyumba.com/host), for signed-in ShambaNyumba users.</summary>
[Authorize, Route("host")]
public class HostController(ICurrentUserService currentUser, ApplicationDbContext db, IConfiguration config, IBookingService bookings, IStayService stays) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["MainSite"] = (config["Booking:MainSiteUrl"] ?? "https://shambanyumba.com").TrimEnd('/');
        ViewData["Back"] = $"{Request.Scheme}://{Request.Host}/host";   // the main site returns here after choosing a business
        // Signed in without a business: straight to ShambaNyumba's "Choose a business" (or create one), then back here.
        if (currentUser.BusinessId is not int businessId)
            return Redirect($"{ViewData["MainSite"]}/Business/Select?returnUrl={Uri.EscapeDataString((string)ViewData["Back"]!)}");
        var business = await db.Businesses.AsNoTracking().Where(b => b.Id == businessId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var units = await db.Units.CountAsync(ct);   // tenant filter: this business's units only
        ViewData["Business"] = business;
        ViewData["Units"] = units;
        ViewData["Stays"] = await db.StayUnits.CountAsync(ct);
        ViewData["Setup"] = await stays.GetSetupProgressAsync(ct);
        return View(await bookings.TodayAsync(ct));
    }

    /// <summary>Signs out of both sites: they share one sign-in cookie.</summary>
    [HttpPost("signout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutOfBoth(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        return Redirect("/");
    }

    /// <summary>Getting started as a host: property, units, photos, rates, publish, how guests pay. Also "Become a host".</summary>
    [HttpGet("start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        ViewData["MainSite"] = (config["Booking:MainSiteUrl"] ?? "https://shambanyumba.com").TrimEnd('/');
        ViewData["Back"] = $"{Request.Scheme}://{Request.Host}/host/start";
        if (currentUser.BusinessId is not int businessId)
            return Redirect($"{ViewData["MainSite"]}/Business/Select?returnUrl={Uri.EscapeDataString((string)ViewData["Back"]!)}");
        ViewData["Instructions"] = await db.Businesses.AsNoTracking().Where(b => b.Id == businessId).Select(b => b.PaymentInstructions).FirstOrDefaultAsync(ct);
        ViewData["CanEdit"] = currentUser.Role is RealEstateERP.Core.Enums.BusinessRole.Owner or RealEstateERP.Core.Enums.BusinessRole.Admin;
        var progress = await stays.GetSetupProgressAsync(ct);
        // While setup is unfinished, the booking site shows a "Getting started" bar on every host page.
        if (progress.Complete) Response.Cookies.Delete(RealEstateERP.Booking.Web.Services.SetupSteps.Cookie);
        else Response.Cookies.Append(RealEstateERP.Booking.Web.Services.SetupSteps.Cookie, "1",
            new CookieOptions { Path = "/host", Expires = DateTimeOffset.UtcNow.AddDays(30), HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps });
        return View(progress);
    }

    /// <summary>How guests pay (shown in confirmations and "My booking"): the same setting as ShambaNyumba's Business settings.</summary>
    [HttpPost("payment-instructions"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentInstructions(string? instructions, CancellationToken ct)
    {
        if (currentUser.Role is not (RealEstateERP.Core.Enums.BusinessRole.Owner or RealEstateERP.Core.Enums.BusinessRole.Admin)) return Forbid();
        var business = await db.Businesses.FirstAsync(b => b.Id == currentUser.BusinessId, ct);
        business.PaymentInstructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        await db.SaveChangesAsync(ct);
        TempData["Success"] = "Saved. Guests see this in their confirmation and under My booking.";
        return Redirect("/host/start");
    }

    [HttpGet("no-access")]
    public IActionResult NoAccess() => View();
}
