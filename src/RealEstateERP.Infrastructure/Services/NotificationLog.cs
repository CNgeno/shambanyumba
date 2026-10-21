using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Adds a notification to the caller's unit of work (saved with whatever caused it). A static helper, like
/// CreditLedger, so any service can raise one without depending on another service.
/// </summary>
public static class NotificationLog
{
    public static void Add(ApplicationDbContext db, int businessId, NotificationType type, string title, string? body = null, string? url = null)
        => db.Notifications.Add(new Notification
        {
            BusinessId = businessId,
            Type = type,
            Title = title.Length > 150 ? title[..150] : title,
            Body = body is { Length: > 500 } ? body[..500] : body,
            Url = url
        });
}
