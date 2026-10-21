using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>Public marketing site (uses _PublicLayout via Views/Home/_ViewStart.cshtml).</summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
public class HomeController(IPlanService plans, ISystemConfigService config, IMarketplaceService marketplace, ILogger<HomeController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        ViewData["Latest"] = await marketplace.LatestAsync(3, ct);
        return View();
    }

    public IActionResult Features() => View();

    /// <summary>The guided tour: landlord, developer and agency journeys, plus the downloadable walkthrough.</summary>
    [HttpGet("tour")]
    public IActionResult Tour() => View();

    public async Task<IActionResult> Pricing(CancellationToken ct)
        => View(new PricingViewModel
        {
            Plans = (await plans.GetAllAsync(ct)).Where(p => p.IsActive).ToList(),
            TrialPlanCode = await config.GetAsync(ConfigKeys.TrialPlanCode, ct) ?? ""
        });

    public IActionResult About() => View();

    public async Task<IActionResult> Contact(CancellationToken ct)
        => View(new ContactViewModel
        {
            Email = await config.GetAsync(ConfigKeys.SupportEmail, ct) ?? "",
            Phone = await config.GetAsync(ConfigKeys.SupportPhone, ct) ?? ""
        });

    public IActionResult Privacy() => View();

    public IActionResult Terms() => View();

    /// <summary>
    /// The friendly error page. Shows a reference and logs the same reference (in the message text, so a plain search
    /// of the log stream, log files or Application Insights finds it) together with the exception, page and user.
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var reference = HttpContext.TraceIdentifier;
        var failure = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature>();
        if (failure?.Error is { } error)
            logger.LogError(error, "Unhandled error, reference {Reference} (trace {TraceId}): {Method} {Path} for {User}",
                reference, System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "-", Request.Method, failure.Path,
                User.Identity?.IsAuthenticated == true ? User.Identity.Name : "anonymous");
        ViewData["Reference"] = reference;
        ViewData["Time"] = DateTime.UtcNow.AddHours(3).ToString("d MMM yyyy, HH:mm:ss") + " EAT";
        return View();
    }
}
