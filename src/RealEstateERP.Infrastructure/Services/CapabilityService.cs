using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>What the current business uses, from its properties and agency setting. Cached 5 minutes per business.</summary>
public sealed class CapabilityService(ApplicationDbContext db, ISubscriptionService subscriptions, IMemoryCache cache, ICurrentUserService currentUser) : ICapabilityService
{
    public static string CacheKey(int businessId) => $"capabilities:{businessId}";

    public async Task<Capabilities> GetAsync(CancellationToken ct = default)
    {
        if (currentUser.BusinessId is not int businessId) return Capabilities.None;
        return (await cache.GetOrCreateAsync(CacheKey(businessId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var assets = db.Assets.AsNoTracking().Where(a => a.LifecycleState != AssetLifecycle.Inactive);
            var hasProperties = await assets.AnyAsync(ct);
            // Purpose null = older rows: land is for sale, everything else to let (same as PropertyRules.Effective).
            var forSale = await assets.AnyAsync(a => a.Purpose == PropertyPurpose.Sell || a.Purpose == PropertyPurpose.Both
                                                     || (a.Purpose == null && a.Type == AssetType.Land), ct);
            var toLet = await assets.AnyAsync(a => a.Purpose == PropertyPurpose.Let || a.Purpose == PropertyPurpose.Both
                                                   || (a.Purpose == null && a.Type != AssetType.Land), ct);
            var agencyOn = await db.Businesses.AsNoTracking().AnyAsync(b => b.Id == businessId && b.Type == BusinessType.Agency, ct);

            var snapshot = await subscriptions.GetSnapshotAsync(businessId, ct);
            var includes = await db.SubscriptionPlans.AsNoTracking().Where(p => p.Code == snapshot.PlanCode).Select(p => p.IncludesAgency).FirstOrDefaultAsync(ct);
            // Menus appear with the properties that need them: letting with the first property to let, selling with the first for sale.
            return new Capabilities(hasProperties, toLet, forSale, agencyOn, PlanRules.AllowsAgency(snapshot.PlanCode, includes, snapshot.IsTrial));
        }))!;
    }

    public void Invalidate(int businessId) => cache.Remove(CacheKey(businessId));

    private static string MemberKey(int businessId, string userId) => $"financials:{businessId}:{userId}";

    public async Task<bool> CanViewFinancialsAsync(CancellationToken ct = default)
    {
        if (currentUser.BusinessId is not int businessId) return false;
        if (currentUser.Role is BusinessRole.Owner or BusinessRole.Admin) return true;
        if (currentUser.UserId is not string userId) return false;
        return await cache.GetOrCreateAsync(MemberKey(businessId, userId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return await db.BusinessUsers.AsNoTracking().AnyAsync(m => m.BusinessId == businessId && m.UserId == userId && m.IsActive && m.CanViewFinancials, ct);
        });
    }

    public void InvalidateMember(int businessId, string userId) => cache.Remove(MemberKey(businessId, userId));
}
