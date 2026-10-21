using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.ViewComponents;

public sealed record NotificationBellModel(int Unread, IReadOnlyList<NotificationRow> Recent);

/// <summary>The bell in the top bar: unread count and the latest few notifications.</summary>
public sealed class NotificationBellViewComponent(INotificationService notifications, ICurrentUserService currentUser) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (currentUser.BusinessId is null) return Content(string.Empty);
        return View(new NotificationBellModel(await notifications.UnreadCountAsync(), await notifications.RecentAsync(6)));
    }
}
