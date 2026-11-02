using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>The host area (booking.shambanyumba.com/host), for signed-in ShambaNyumba users.</summary>
[Authorize, Route("host")]
public class HostController(ICurrentUserService currentUser, ApplicationDbContext db, IConfiguration config, IBookingService bookings) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["MainSite"] = (config["Booking:MainSiteUrl"] ?? "https://shambanyumba.com").TrimEnd('/');
        ViewData["Back"] = $"{Request.Scheme}://{Request.Host}/host";   // the main site returns here after choosing a business
        if (currentUser.BusinessId is not int businessId) return View("ChooseBusiness");
        var business = await db.Businesses.AsNoTracking().Where(b => b.Id == businessId).Select(b => b.Name).FirstOrDefaultAsync(ct);
        var units = await db.Units.CountAsync(ct);   // tenant filter: this business's units only
        ViewData["Business"] = business;
        ViewData["Units"] = units;
        ViewData["Stays"] = await db.StayUnits.CountAsync(ct);
        return View(await bookings.TodayAsync(ct));
    }

    /// <summary>Signs out of both sites: they share one sign-in cookie.</summary>
    [HttpPost("signout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutOfBoth(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        return Redirect("/");
    }

    [HttpGet("no-access")]
    public IActionResult NoAccess() => View();
}
