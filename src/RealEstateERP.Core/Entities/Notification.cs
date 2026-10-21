using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-14-A in-app notification for everyone in a business. Platform-level (written by anonymous visitors, the daily
/// job and the platform admin), so always queried by BusinessId. Unread = newer than the member's NotificationsSeenAtUtc.
/// </summary>
public class Notification : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    /// <summary>App-relative link, e.g. /Leads.</summary>
    public string? Url { get; set; }
}
