using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// Membership of a user in a business (FR-01-C/D).
/// Deliberately NOT an ITenantEntity: the business-selection screen must list
/// memberships across all businesses before one is chosen.
/// </summary>
public class BusinessUser : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    /// <summary>AspNetUsers.Id. No navigation property, so Core stays free of Identity.</summary>
    public string UserId { get; set; } = string.Empty;

    public BusinessRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>When this member last opened their notifications; newer ones count as unread.</summary>
    public DateTime? NotificationsSeenAtUtc { get; set; }

    /// <summary>Users and Viewers: may see reports, costs and margins, expenses and net income. Owners and Admins always can.</summary>
    public bool CanViewFinancials { get; set; }
}
