using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// FR-01-E. An invitation to join a business with a role. Not an ITenantEntity: the person accepting it has no
/// business selected yet. Only a SHA-256 hash of the token is stored, so a database leak can't be used to join.
/// </summary>
public class BusinessInvitation : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    /// <summary>Upper-case, trimmed.</summary>
    public string Email { get; set; } = string.Empty;
    public BusinessRole Role { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }
    public string? AcceptedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsOpen(DateTime nowUtc) => AcceptedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
