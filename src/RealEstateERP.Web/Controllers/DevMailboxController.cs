using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>
/// Read the emails the app would have sent. Development: anyone. Test sites (Site:IsTest): super admins only, because the
/// mailbox holds password-reset links and real people's details from the copied data. Live: 404.
/// </summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("dev/mailbox")]
public class DevMailboxController(IDevMailbox mailbox, IWebHostEnvironment env, IConfiguration config, IAuthorizationService authorization) : Controller
{
    private async Task<bool> AllowedAsync()
        => env.IsDevelopment()
           || (RealEstateERP.Infrastructure.TestSite.IsTest(config) && (await authorization.AuthorizeAsync(User, Policies.SuperAdmin)).Succeeded);

    [HttpGet("")]
    public async Task<IActionResult> Index(int? id)
    {
        if (!await AllowedAsync()) return NotFound();
        var emails = mailbox.All();
        return View(new DevMailboxViewModel { Emails = emails, Selected = id is int i ? mailbox.Get(i) : emails.FirstOrDefault() });
    }

    [HttpGet("{id:int}/body")]
    public async Task<IActionResult> Body(int id)
    {
        if (!await AllowedAsync()) return NotFound();
        var email = mailbox.Get(id);
        return email is null ? NotFound() : Content(email.HtmlBody, "text/html");
    }

    [HttpPost("clear"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Clear()
    {
        if (!await AllowedAsync()) return NotFound();
        mailbox.Clear();
        return RedirectToAction(nameof(Index));
    }
}
