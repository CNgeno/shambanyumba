using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Models;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

/// <summary>DEVELOPMENT ONLY: read the emails the app would have sent. 404 in every other environment.</summary>
[AllowAnonymous, AllowWithoutBusiness, SkipSubscriptionCheck]
[Route("dev/mailbox")]
public class DevMailboxController(IDevMailbox mailbox, IWebHostEnvironment env) : Controller
{
    [HttpGet("")]
    public IActionResult Index(int? id)
    {
        if (!env.IsDevelopment()) return NotFound();
        var emails = mailbox.All();
        return View(new DevMailboxViewModel { Emails = emails, Selected = id is int i ? mailbox.Get(i) : emails.FirstOrDefault() });
    }

    [HttpGet("{id:int}/body")]
    public IActionResult Body(int id)
    {
        if (!env.IsDevelopment()) return NotFound();
        var email = mailbox.Get(id);
        return email is null ? NotFound() : Content(email.HtmlBody, "text/html");
    }

    [HttpPost("clear"), IgnoreAntiforgeryToken]
    public IActionResult Clear()
    {
        if (!env.IsDevelopment()) return NotFound();
        mailbox.Clear();
        return RedirectToAction(nameof(Index));
    }
}
