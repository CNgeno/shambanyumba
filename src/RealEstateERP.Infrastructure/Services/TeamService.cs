using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-01-E. Depends on: DbContext, IEmailService, ISystemConfigService (low-level), ICurrentUserService, TimeProvider.
/// Invariants: every business keeps at least one Owner; only Owners can grant, change or remove the Owner role.
/// </summary>
public sealed class TeamService(
    ApplicationDbContext db,
    IEmailService email,
    ISystemConfigService config,
    ICurrentUserService currentUser,
    TimeProvider clock,
    ICapabilityService? capabilities = null) : ITeamService
{
    private static readonly TimeSpan Validity = TimeSpan.FromDays(7);

    private int BusinessId => currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");
    private bool IsOwner => currentUser.Role == BusinessRole.Owner;
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ members

    public async Task<IReadOnlyList<TeamMember>> GetMembersAsync(CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var rows = await (from m in db.BusinessUsers.AsNoTracking()
                          join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                          where m.BusinessId == businessId && m.IsActive
                          select new { m.Id, u.Email, u.FullName, m.Role, m.UserId, m.CreatedAtUtc, m.CanViewFinancials })
            .ToListAsync(ct);
        return rows
            .OrderBy(r => r.Role).ThenBy(r => r.FullName ?? r.Email)
            .Select(r => new TeamMember(r.Id, r.UserId, r.Email ?? "", r.FullName, r.Role, r.UserId == currentUser.UserId, r.CreatedAtUtc, r.CanViewFinancials))
            .ToList();
    }

    public async Task<IReadOnlyList<InvitationSummary>> GetOpenInvitationsAsync(CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var now = Now;
        var rows = await db.BusinessInvitations.AsNoTracking()
            .Where(i => i.BusinessId == businessId && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new { i.Id, i.Email, i.Role, i.CreatedAtUtc, i.UpdatedAtUtc, i.ExpiresAtUtc, i.CreatedBy })
            .ToListAsync(ct);
        var inviterIds = rows.Select(r => r.CreatedBy).Where(id => id != null).Distinct().ToList();
        var inviters = await db.Users.AsNoTracking().Where(u => inviterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.Email, ct);
        return rows.Select(r => new InvitationSummary(r.Id, r.Email.ToLowerInvariant(), r.Role, r.UpdatedAtUtc ?? r.CreatedAtUtc, r.ExpiresAtUtc,
            r.CreatedBy is not null && inviters.TryGetValue(r.CreatedBy, out var name) ? name : null)).ToList();
    }

    // ------------------------------------------------------------------ invitations (admin side)

    public async Task<Result> InviteAsync(string emailAddress, BusinessRole role, string acceptUrlFormat, CancellationToken ct = default)
    {
        var normalized = Normalize(emailAddress);
        if (normalized is null) return Result.Fail("Enter a valid email address.");
        if (role == BusinessRole.Owner && !IsOwner) return Result.Fail("Only an Owner can invite another Owner.");

        var businessId = BusinessId;
        var existingUserId = await db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (existingUserId is not null && await db.BusinessUsers.AnyAsync(m => m.BusinessId == businessId && m.UserId == existingUserId && m.IsActive, ct))
            return Result.Fail("That person is already a member of this business.");
        var now = Now;
        if (await db.BusinessInvitations.AnyAsync(i => i.BusinessId == businessId && i.Email == normalized
                && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now, ct))
            return Result.Fail("There's already an open invitation for that email. Use Resend instead.");

        var token = NewToken();
        var invitation = new BusinessInvitation
        {
            BusinessId = businessId, Email = normalized, Role = role, TokenHash = Hash(token), ExpiresAtUtc = now.Add(Validity)
        };
        db.BusinessInvitations.Add(invitation);
        await db.SaveChangesAsync(ct);
        return await SendInvitationAsync(invitation, token, acceptUrlFormat, ct);
    }

    public async Task<Result> ResendInvitationAsync(int invitationId, string acceptUrlFormat, CancellationToken ct = default)
    {
        var invitation = await LoadOwnInvitationAsync(invitationId, ct);
        if (invitation is null || !invitation.IsOpen(Now)) return Result.Fail("That invitation is no longer open.");
        if (invitation.Role == BusinessRole.Owner && !IsOwner) return Result.Fail("Only an Owner can resend an Owner invitation.");

        var token = NewToken();   // the old link stops working
        invitation.TokenHash = Hash(token);
        invitation.ExpiresAtUtc = Now.Add(Validity);
        await db.SaveChangesAsync(ct);
        return await SendInvitationAsync(invitation, token, acceptUrlFormat, ct);
    }

    public async Task<Result> RevokeInvitationAsync(int invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadOwnInvitationAsync(invitationId, ct);
        if (invitation is null || !invitation.IsOpen(Now)) return Result.Fail("That invitation is no longer open.");
        if (invitation.Role == BusinessRole.Owner && !IsOwner) return Result.Fail("Only an Owner can cancel an Owner invitation.");
        invitation.RevokedAtUtc = Now;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ roles

    public async Task<Result> ChangeRoleAsync(int membershipId, BusinessRole role, CancellationToken ct = default)
    {
        var member = await db.BusinessUsers.FirstOrDefaultAsync(m => m.Id == membershipId && m.BusinessId == BusinessId && m.IsActive, ct);
        if (member is null) return Result.Fail("Member not found.");
        if (member.Role == role) return Result.Ok();
        if ((member.Role == BusinessRole.Owner || role == BusinessRole.Owner) && !IsOwner)
            return Result.Fail("Only an Owner can give or take away the Owner role.");
        if (member.Role == BusinessRole.Owner && await OwnerCountAsync(ct) == 1)
            return Result.Fail("A business must always have at least one Owner. Make someone else an Owner first.");

        member.Role = role;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetFinancialAccessAsync(int membershipId, bool canView, CancellationToken ct = default)
    {
        if (currentUser.Role is not (BusinessRole.Owner or BusinessRole.Admin)) return Result.Fail("Only an Owner or Admin can change this.");
        var member = await db.BusinessUsers.FirstOrDefaultAsync(m => m.Id == membershipId && m.BusinessId == BusinessId && m.IsActive, ct);
        if (member is null) return Result.Fail("Member not found.");
        if (member.Role is BusinessRole.Owner or BusinessRole.Admin) return Result.Fail("Owners and Admins always see finances.");
        member.CanViewFinancials = canView;
        await db.SaveChangesAsync(ct);
        capabilities?.InvalidateMember(member.BusinessId, member.UserId);
        return Result.Ok();
    }

    public async Task<Result> RemoveAsync(int membershipId, CancellationToken ct = default)
    {
        var member = await db.BusinessUsers.FirstOrDefaultAsync(m => m.Id == membershipId && m.BusinessId == BusinessId && m.IsActive, ct);
        if (member is null) return Result.Fail("Member not found.");
        if (member.Role == BusinessRole.Owner && !IsOwner) return Result.Fail("Only an Owner can remove an Owner.");
        if (member.Role == BusinessRole.Owner && await OwnerCountAsync(ct) == 1)
            return Result.Fail("A business must always have at least one Owner.");

        db.BusinessUsers.Remove(member);   // soft delete; they lose access within a couple of minutes at most
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ invitee side

    public async Task<InvitationLinkInfo?> DescribeTokenAsync(string token, CancellationToken ct = default)
    {
        var hash = Hash(token);
        var invitation = await db.BusinessInvitations.AsNoTracking().Include(i => i.Business).FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        return invitation is null ? null
            : new InvitationLinkInfo(invitation.Business.Name, invitation.Role, invitation.Email.ToLowerInvariant(), invitation.IsOpen(Now));
    }

    public async Task<IReadOnlyList<PendingInvitation>> GetInvitationsForUserAsync(string userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId && u.EmailConfirmed).Select(u => new { u.NormalizedEmail }).FirstOrDefaultAsync(ct);
        if (user?.NormalizedEmail is null) return [];
        var now = Now;
        return await db.BusinessInvitations.AsNoTracking()
            .Where(i => i.Email == user.NormalizedEmail && i.AcceptedAtUtc == null && i.RevokedAtUtc == null && i.ExpiresAtUtc > now
                        && !db.BusinessUsers.Any(m => m.BusinessId == i.BusinessId && m.UserId == userId && m.IsActive))
            .OrderBy(i => i.Business.Name)
            .Select(i => new PendingInvitation(i.Id, i.BusinessId, i.Business.Name, i.Role, i.ExpiresAtUtc))
            .ToListAsync(ct);
    }

    public async Task<Result<int>> AcceptByTokenAsync(string token, string userId, CancellationToken ct = default)
    {
        var hash = Hash(token);
        return await AcceptAsync(await db.BusinessInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct), userId, ct);
    }

    public async Task<Result<int>> AcceptByIdAsync(int invitationId, string userId, CancellationToken ct = default)
        => await AcceptAsync(await db.BusinessInvitations.FirstOrDefaultAsync(i => i.Id == invitationId, ct), userId, ct);

    // ------------------------------------------------------------------ helpers

    private async Task<Result<int>> AcceptAsync(BusinessInvitation? invitation, string userId, CancellationToken ct)
    {
        if (invitation is null) return Result<int>.Fail("This invitation link isn't valid.");
        if (!invitation.IsOpen(Now)) return Result<int>.Fail("This invitation has expired or been cancelled. Ask for a new one.");

        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.NormalizedEmail, u.EmailConfirmed }).FirstOrDefaultAsync(ct);
        if (user is null || !user.EmailConfirmed) return Result<int>.Fail("Confirm your email address first.");
        if (user.NormalizedEmail != invitation.Email)
            return Result<int>.Fail($"This invitation was sent to {invitation.Email.ToLowerInvariant()}. Sign in with that email address to accept it.");

        if (!await db.BusinessUsers.AnyAsync(m => m.BusinessId == invitation.BusinessId && m.UserId == userId && m.IsActive, ct))
            db.BusinessUsers.Add(new BusinessUser { BusinessId = invitation.BusinessId, UserId = userId, Role = invitation.Role });

        invitation.AcceptedAtUtc = Now;
        invitation.AcceptedByUserId = userId;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(invitation.BusinessId);
    }

    private async Task<Result> SendInvitationAsync(BusinessInvitation invitation, string token, string acceptUrlFormat, CancellationToken ct)
    {
        var platform = await config.GetAsync(ConfigKeys.PlatformName, ct) ?? "ShambaNyumba";
        var businessName = await db.Businesses.Where(b => b.Id == invitation.BusinessId).Select(b => b.Name).FirstAsync(ct);
        var inviter = await db.Users.Where(u => u.Id == currentUser.UserId).Select(u => u.FullName ?? u.Email).FirstOrDefaultAsync(ct);
        var url = string.Format(acceptUrlFormat, Uri.EscapeDataString(token));

        var html = EmailTemplates.Render(platform, $"You're invited to {businessName}",
            [$"{inviter ?? "A colleague"} has invited you to join {businessName} on {platform} as {invitation.Role}.",
             "Accept the invitation to get access. If you don't have an account yet, you can create one with this email address."],
            "Accept invitation", url, $"This invitation expires on {invitation.ExpiresAtUtc.AddHours(3):d MMM yyyy}. If you weren't expecting it, ignore this email.");
        try
        {
            await email.SendAsync(new EmailMessage(invitation.Email.ToLowerInvariant(), $"Invitation to join {businessName}", html), ct);
            return Result.Ok();
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            return Result.Fail("The invitation was saved, but the email couldn't be sent. Check the email settings, then use Resend.");
        }
    }

    private Task<BusinessInvitation?> LoadOwnInvitationAsync(int id, CancellationToken ct)
        => db.BusinessInvitations.FirstOrDefaultAsync(i => i.Id == id && i.BusinessId == BusinessId, ct);

    private Task<int> OwnerCountAsync(CancellationToken ct)
        => db.BusinessUsers.CountAsync(m => m.BusinessId == BusinessId && m.IsActive && m.Role == BusinessRole.Owner, ct);

    private static string? Normalize(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        try { return new MailAddress(address.Trim()).Address.ToUpperInvariant(); }
        catch (FormatException) { return null; }
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim().ToUpperInvariant())));
}
