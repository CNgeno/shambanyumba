using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record TeamMember(int MembershipId, string UserId, string Email, string? FullName, BusinessRole Role, bool IsYou, DateTime JoinedAtUtc, bool CanViewFinancials = false)
{
    public bool SeesFinances => Role is BusinessRole.Owner or BusinessRole.Admin || CanViewFinancials;
}

public sealed record InvitationSummary(int Id, string Email, BusinessRole Role, DateTime SentAtUtc, DateTime ExpiresAtUtc, string? InvitedBy);

/// <summary>An open invitation shown to the invitee (on the business selection page).</summary>
public sealed record PendingInvitation(int Id, int BusinessId, string BusinessName, BusinessRole Role, DateTime ExpiresAtUtc);

public sealed record InvitationLinkInfo(string BusinessName, BusinessRole Role, string Email, bool IsOpen);

public sealed record BusinessProfileInput(string Name, BusinessType Type, string? Email, string? Phone, string? PhysicalAddress, string? KraPin, bool? ShowRentalTools = null);

public sealed record AdminUserSummary(
    string Id, string Email, string? FullName, bool EmailConfirmed, bool IsLockedOut, bool IsSuperAdmin,
    int BusinessCount, DateTime CreatedAtUtc, DateTime? LastLoginAtUtc);

public sealed record AdminBusinessSummary(
    int Id, string Name, BusinessType Type, string? OwnerEmail, int MemberCount, int CreditBalance, DateTime CreatedAtUtc);
