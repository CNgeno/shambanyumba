using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Platform admin directory. Read-only. Depends on: DbContext, TimeProvider.</summary>
public sealed class AdminDirectoryService(ApplicationDbContext db, TimeProvider clock) : IAdminDirectoryService
{
    public async Task<IReadOnlyList<AdminUserSummary>> GetUsersAsync(string? search, CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u => (u.Email != null && u.Email.Contains(term)) || (u.FullName != null && u.FullName.Contains(term)));
        }
        var users = await query.OrderByDescending(u => u.CreatedAtUtc).Take(500)
            .Select(u => new { u.Id, u.Email, u.FullName, u.EmailConfirmed, u.LockoutEnd, u.CreatedAtUtc, u.LastLoginAtUtc })
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();

        var superAdmins = (await (from ur in db.UserRoles
                                  join r in db.Roles on ur.RoleId equals r.Id
                                  where r.Name == AppRoles.SuperAdmin && ids.Contains(ur.UserId)
                                  select ur.UserId).ToListAsync(ct)).ToHashSet();
        var memberships = await db.BusinessUsers.AsNoTracking()
            .Where(m => ids.Contains(m.UserId) && m.IsActive && !m.Business.IsDeleted)
            .GroupBy(m => m.UserId).Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var now = clock.GetUtcNow();
        return users.Select(u => new AdminUserSummary(u.Id, u.Email ?? "", u.FullName, u.EmailConfirmed, u.LockoutEnd > now,
            superAdmins.Contains(u.Id), memberships.GetValueOrDefault(u.Id), u.CreatedAtUtc, u.LastLoginAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<AdminBusinessSummary>> GetBusinessesAsync(string? search, CancellationToken ct = default)
    {
        var query = db.Businesses.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(b => b.Name.Contains(search.Trim()));

        var businesses = await query.OrderBy(b => b.Name).Take(500)
            .Select(b => new { b.Id, b.Name, b.Type, b.CreditBalance, b.CreatedAtUtc, Members = b.Members.Count(m => m.IsActive) })
            .ToListAsync(ct);
        var ids = businesses.Select(b => b.Id).ToList();
        var owners = await (from m in db.BusinessUsers.AsNoTracking()
                            join u in db.Users.AsNoTracking() on m.UserId equals u.Id
                            where ids.Contains(m.BusinessId) && m.IsActive && m.Role == BusinessRole.Owner
                            orderby m.CreatedAtUtc
                            select new { m.BusinessId, u.Email }).ToListAsync(ct);
        var ownerByBusiness = owners.GroupBy(o => o.BusinessId).ToDictionary(g => g.Key, g => g.First().Email);

        return businesses.Select(b => new AdminBusinessSummary(b.Id, b.Name, b.Type, ownerByBusiness.GetValueOrDefault(b.Id),
            b.Members, b.CreditBalance, b.CreatedAtUtc)).ToList();
    }
}
