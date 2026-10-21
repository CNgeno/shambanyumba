using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Identity;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>The public demo: one click, no password. Anyone can sign in; everything resets every night.</summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("demo")]
public class DemoController(SignInManager<ApplicationUser> signIn, IPublicDemoService demo, IConfiguration config, ILogger<DemoController> logger) : Controller
{
    /// <summary>Start the demo as the owner of the three demo businesses, or as an agent at the demo agency.</summary>
    [HttpPost("start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string? @as)
    {
        if (!config.GetValue("Demo:PublicEnabled", true)) return NotFound();
        try
        {
            var (ownerId, agentId) = await demo.EnsureAsync(HttpContext.RequestAborted);
            var user = await signIn.UserManager.FindByIdAsync(@as == "agent" ? agentId : ownerId)
                       ?? throw new InvalidOperationException("Demo login missing.");
            await signIn.SignOutAsync();
            await signIn.SignInAsync(user, isPersistent: false);
            return Redirect("/Business/Select");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The public demo couldn't start.");
            TempData["Error"] = "The demo isn't available right now. Please try again in a few minutes, or start a free trial.";
            return Redirect("/");
        }
    }

    /// <summary>Shown when a demo login tries something the demo doesn't allow (account, payments, settings).</summary>
    [HttpGet("not-in-demo")]
    public IActionResult NotInDemo() => View();
}
