using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// FR-01-E team management. Depends on: DbContext, IEmailService, ICurrentUserService, ISystemConfigService, TimeProvider.
/// Rules: a business always keeps at least one Owner; only Owners can add, change or remove Owners.
/// </summary>
public interface ITeamService
{
    Task<IReadOnlyList<TeamMember>> GetMembersAsync(CancellationToken ct = default);
    Task<IReadOnlyList<InvitationSummary>> GetOpenInvitationsAsync(CancellationToken ct = default);

    /// <param name="acceptUrlFormat">Absolute URL with {0} where the token goes.</param>
    Task<Result> InviteAsync(string email, BusinessRole role, string acceptUrlFormat, CancellationToken ct = default);
    Task<Result> ResendInvitationAsync(int invitationId, string acceptUrlFormat, CancellationToken ct = default);
    Task<Result> RevokeInvitationAsync(int invitationId, CancellationToken ct = default);
    Task<Result> ChangeRoleAsync(int membershipId, BusinessRole role, CancellationToken ct = default);
    Task<Result> RemoveAsync(int membershipId, CancellationToken ct = default);
    /// <summary>Owner or Admin: let a User or Viewer see reports, costs, expenses and net income (or not).</summary>
    Task<Result> SetFinancialAccessAsync(int membershipId, bool canView, CancellationToken ct = default);

    // Invitee side (no business selected)
    Task<InvitationLinkInfo?> DescribeTokenAsync(string token, CancellationToken ct = default);
    Task<IReadOnlyList<PendingInvitation>> GetInvitationsForUserAsync(string userId, CancellationToken ct = default);
    Task<Result<int>> AcceptByTokenAsync(string token, string userId, CancellationToken ct = default);
    Task<Result<int>> AcceptByIdAsync(int invitationId, string userId, CancellationToken ct = default);
}

/// <summary>Platform admin directory of users and businesses. Depends on: DbContext, TimeProvider.</summary>
public interface IAdminDirectoryService
{
    Task<IReadOnlyList<AdminUserSummary>> GetUsersAsync(string? search, CancellationToken ct = default);
    Task<IReadOnlyList<AdminBusinessSummary>> GetBusinessesAsync(string? search, CancellationToken ct = default);
}
