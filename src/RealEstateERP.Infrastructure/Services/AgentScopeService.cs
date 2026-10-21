using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// LOW-LEVEL. A member linked to an agent profile with the User or Viewer role is restricted to that agent's
/// contracts. Owners (Principals) and Admins are never restricted, even if linked. An inactive agent stays
/// restricted (deactivating must never widen what someone can see).
/// </summary>
public sealed class AgentScopeService(ApplicationDbContext db, ICurrentUserService currentUser, IMemoryCache cache) : IAgentScopeService
{
    private static string Key(int businessId, string userId) => $"agent-scope:{businessId}:{userId}";

    public async Task<AgentScope> GetAsync(CancellationToken ct = default)
    {
        if (currentUser.BusinessType != BusinessType.Agency || currentUser.BusinessId is not int businessId || currentUser.UserId is not string userId)
            return AgentScope.None;

        var linked = await cache.GetOrCreateAsync(Key(businessId, userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await db.Agents.AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => new AgentScope(a.Id, a.FullName, false))
                .FirstOrDefaultAsync(ct) ?? AgentScope.None;
        }) ?? AgentScope.None;

        if (linked.AgentId is null) return AgentScope.None;
        var restricted = currentUser.Role is BusinessRole.User or BusinessRole.Viewer;
        return linked with { IsRestricted = restricted };
    }

    public void Invalidate(int businessId, string userId) => cache.Remove(Key(businessId, userId));
}
