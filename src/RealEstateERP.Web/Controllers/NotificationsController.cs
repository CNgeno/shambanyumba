using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Web.Security;

namespace RealEstateERP.Web.Controllers;

[SkipSubscriptionCheck]
public class NotificationsController(INotificationService notifications) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await notifications.RecentAsync(100, ct);
        await notifications.MarkAllReadAsync(ct);   // opening the list counts as reading it
        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(string? returnUrl, CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }
}
