using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// An enquiry from the public site. Platform-level (NOT an ITenantEntity): it's created and verified by anonymous
/// visitors, so it's always queried with an explicit BusinessId. Only a SHA-256 hash of the email token is stored.
/// </summary>
public class ListingLead : AuditableEntity
{
    public int BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>+2547... form.</summary>
    public string Phone { get; set; } = string.Empty;
    /// <summary>Lower-case.</summary>
    public string Email { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime ConsentedAtUtc { get; set; }

    public LeadStatus Status { get; set; } = LeadStatus.Unverified;
    public LeadStage Stage { get; set; } = LeadStage.New;
    public string? VerificationTokenHash { get; set; }
    public DateTime VerifyBeforeUtc { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }

    /// <summary>Same person, same listing, within 30 days of a charged lead: shown free.</summary>
    public bool IsRepeat { get; set; }
    public int CreditsCharged { get; set; }
    public DateTime? UnlockedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public string? RefundReason { get; set; }
    public string? Notes { get; set; }
}
