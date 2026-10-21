using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Web.Formatting;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>
/// FR-12 public marketplace at /marketplace. Anonymous; reads every business's live listings.
/// (Not /properties: URLs are case-insensitive, so that clashed with the app's own Properties pages.)
/// </summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("marketplace")]
public class PropertiesPublicController(IMarketplaceService marketplace, IFileStorage storage, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider protection) : Controller
{
    public const string EnquiryLimit = "enquiries";

    [HttpGet("")]
    public async Task<IActionResult> Index(ListingType? type, string? county, string? q, decimal? minPrice, decimal? maxPrice, int? beds, UnitType? unitType, int page = 1, CancellationToken ct = default)
    {
        var search = new PublicSearch(type, county, q, minPrice, maxPrice, beds, unitType, page);
        return View("~/Views/PropertiesPublic/Index.cshtml", new MarketSearchViewModel { Search = search, Result = await marketplace.SearchAsync(search, ct) });
    }

    [HttpGet("{id:int}/{slug?}")]
    public async Task<IActionResult> Details(int id, string? slug, bool sent = false, bool delivered = false, CancellationToken ct = default)
    {
        var details = await marketplace.GetAsync(id, countView: !sent && !delivered, ct);
        if (details is null) return View("~/Views/PropertiesPublic/Gone.cshtml");
        if (slug != details.Card.Slug) return RedirectToActionPermanent(nameof(Details), new { id, slug = details.Card.Slug });
        return View("~/Views/PropertiesPublic/Details.cshtml", new MarketListingViewModel { Details = details, Form = new EnquiryForm { ListingId = id, Email = ConfirmedEmail() ?? "" }, Sent = sent, Delivered = delivered });
    }

    [HttpPost("{id:int}/enquire"), ValidateAntiForgeryToken, EnableRateLimiting(EnquiryLimit)]
    public async Task<IActionResult> Enquire(int id, EnquiryForm form, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(form.Website)) return RedirectToAction(nameof(Details), new { id, sent = true });   // bot: pretend it worked

        var details = await marketplace.GetAsync(id, countView: false, ct);
        if (details is null) return View("~/Views/PropertiesPublic/Gone.cshtml");

        if (ModelState.IsValid)
        {
            var verifyUrl = Url.Action(nameof(Verify), "PropertiesPublic", new { token = "TOKEN_PLACEHOLDER" }, Request.Scheme)!.Replace("TOKEN_PLACEHOLDER", "{0}");
            var confirmed = ConfirmedEmail() is string c && string.Equals(c, form.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
            var result = await marketplace.SubmitEnquiryAsync(new EnquiryInput(id, form.Name, form.Phone, form.Email ?? "", form.Message, form.Consent, confirmed),
                verifyUrl, LeadsUrl(), ct);
            if (result.Succeeded)
                return result.Value
                    ? RedirectToAction(nameof(Details), new { id, slug = details.Card.Slug, delivered = true })   // went straight to the owner
                    : RedirectToAction(nameof(Details), new { id, slug = details.Card.Slug, sent = true });     // confirmation email sent
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        return View("~/Views/PropertiesPublic/Details.cshtml", new MarketListingViewModel { Details = details, Form = form });
    }

    [HttpGet("verify")]
    public async Task<IActionResult> Verify(string token, CancellationToken ct)
    {
        var result = await marketplace.VerifyAsync(token, LeadsUrl(), ct);
        if (result.Verified && result.Email is string email)
        {
            // Remember the confirmed email in this browser (encrypted), so further enquiries skip the email step.
            var value = Protector.Protect($"{email}|{DateTime.UtcNow.Ticks}");
            Response.Cookies.Append(ConfirmedCookie, value, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(RealEstateERP.Core.Common.MarketplaceRules.ConfirmedEmailValidDays),
                HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = Request.IsHttps
            });
        }
        return View("~/Views/PropertiesPublic/Verify.cshtml", result);
    }

    private const string ConfirmedCookie = "sn_confirmed";
    private Microsoft.AspNetCore.DataProtection.IDataProtector Protector => protection.CreateProtector("ShambaNyumba.ConfirmedEnquiryEmail.v1");

    /// <summary>The email this browser confirmed within the window, or null (missing, tampered, or older than the window).</summary>
    private string? ConfirmedEmail()
    {
        if (Request.Cookies[ConfirmedCookie] is not string raw) return null;
        try
        {
            var parts = Protector.Unprotect(raw).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[1], out var ticks)) return null;
            return new DateTime(ticks, DateTimeKind.Utc) > DateTime.UtcNow.AddDays(-RealEstateERP.Core.Common.MarketplaceRules.ConfirmedEmailValidDays) ? parts[0] : null;
        }
        catch (System.Security.Cryptography.CryptographicException) { return null; }
    }

    [HttpGet("photo/{id:int}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Photo(int id, CancellationToken ct)
    {
        var photo = await marketplace.GetPublicPhotoAsync(id, ct);
        if (photo is null) return NotFound();
        var stream = await storage.OpenReadAsync(photo.Value.Key, ct);
        return stream is null ? NotFound() : File(stream, DisplayFormat.ContentTypeFor(photo.Value.Key));
    }

    private string LeadsUrl() => Url.Action("Index", "Leads", null, Request.Scheme)!;
}
