using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>FR-14-A. Depends on: DbContext, ICurrentUserService, TimeProvider.</summary>
public sealed class NotificationService(ApplicationDbContext db, ICurrentUserService currentUser, TimeProvider clock) : INotificationService
{
    private async Task<(int BusinessId, DateTime? SeenAt)?> MemberAsync(CancellationToken ct)
    {
        if (currentUser.BusinessId is not int businessId || currentUser.UserId is not string userId) return null;
        var seen = await db.BusinessUsers.AsNoTracking().Where(m => m.BusinessId == businessId && m.UserId == userId && m.IsActive)
            .Select(m => new { m.NotificationsSeenAtUtc }).FirstOrDefaultAsync(ct);
        return seen is null ? null : (businessId, seen.NotificationsSeenAtUtc);
    }

    public async Task<IReadOnlyList<NotificationRow>> RecentAsync(int take, CancellationToken ct = default)
    {
        if (await MemberAsync(ct) is not { } m) return [];
        var rows = await db.Notifications.AsNoTracking().Where(n => n.BusinessId == m.BusinessId)
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id).Take(take)
            .Select(n => new { n.Id, n.Type, n.Title, n.Body, n.Url, n.CreatedAtUtc }).ToListAsync(ct);
        return rows.Select(n => new NotificationRow(n.Id, n.Type, n.Title, n.Body, n.Url, n.CreatedAtUtc, m.SeenAt is null || n.CreatedAtUtc > m.SeenAt)).ToList();
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        if (await MemberAsync(ct) is not { } m) return 0;
        var seen = m.SeenAt;
        return await db.Notifications.CountAsync(n => n.BusinessId == m.BusinessId && (seen == null || n.CreatedAtUtc > seen), ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        if (currentUser.BusinessId is not int businessId || currentUser.UserId is not string userId) return;
        var member = await db.BusinessUsers.FirstOrDefaultAsync(m => m.BusinessId == businessId && m.UserId == userId && m.IsActive, ct);
        if (member is null) return;
        member.NotificationsSeenAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }
}
