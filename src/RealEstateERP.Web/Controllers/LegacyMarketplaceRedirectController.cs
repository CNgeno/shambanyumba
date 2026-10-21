using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>
/// The marketplace used to live at /properties. These permanent redirects keep links already shared (listing pages,
/// photos, confirmation emails) working. None of them clash with the app's Properties pages (no numeric or
/// verify/photo actions there).
/// </summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
public class LegacyMarketplaceRedirectController : Controller
{
    [HttpGet("properties/{id:int}/{slug?}")]
    public IActionResult Listing(int id, string? slug) => RedirectPermanent($"/marketplace/{id}/{slug}");

    [HttpGet("properties/photo/{id:int}")]
    public IActionResult Photo(int id) => RedirectPermanent($"/marketplace/photo/{id}");

    [HttpGet("properties/verify")]
    public IActionResult Verify(string? token) => RedirectPermanent($"/marketplace/verify?token={Uri.EscapeDataString(token ?? "")}");
}
