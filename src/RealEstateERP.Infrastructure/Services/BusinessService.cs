using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Depends on: DbContext, ISubscriptionService. (SubscriptionService never depends on this.)</summary>
public sealed class BusinessService(
    ApplicationDbContext db,
    ISubscriptionService subscriptions,
    IFileStorage storage,
    ISystemConfigService config,
    ICapabilityService? capabilities = null) : IBusinessService
{
    public async Task<IReadOnlyList<UserBusinessSummary>> GetBusinessesForUserAsync(string userId, CancellationToken ct = default)
        => await db.BusinessUsers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive && !m.Business.IsDeleted)
            .OrderBy(m => m.Business.Name)
            .Select(m => new UserBusinessSummary(m.BusinessId, m.Business.Name, m.Business.Type, m.Role))
            .ToListAsync(ct);

    public Task<UserBusinessSummary?> GetMembershipAsync(string userId, int businessId, CancellationToken ct = default)
        => db.BusinessUsers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.BusinessId == businessId && m.IsActive && !m.Business.IsDeleted)
            .Select(m => new UserBusinessSummary(m.BusinessId, m.Business.Name, m.Business.Type, m.Role))
            .FirstOrDefaultAsync(ct);

    public Task<BusinessProfile?> GetProfileAsync(int businessId, CancellationToken ct = default)
        => db.Businesses.AsNoTracking().Where(b => b.Id == businessId)
            .Select(b => new BusinessProfile(b.Name, b.Phone, b.Email, b.KraPin, b.PhysicalAddress) { LogoKey = b.LogoPath, Type = b.Type, ShowRentalTools = b.ShowRentalTools })
            .FirstOrDefaultAsync(ct);

    public async Task<Result> UpdateProfileAsync(int businessId, BusinessProfileInput input, CancellationToken ct = default)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business is null) return Result.Fail("Business not found.");
        if (string.IsNullOrWhiteSpace(input.Name)) return Result.Fail("Enter the business name.");

        business.Name = input.Name.Trim();
        if (input.Type == BusinessType.Agency && business.Type != BusinessType.Agency)
        {
            var snapshot = await subscriptions.GetSnapshotAsync(business.Id, ct);
            var includes = await db.SubscriptionPlans.AsNoTracking().Where(p => p.Code == snapshot.PlanCode).Select(p => p.IncludesAgency).FirstOrDefaultAsync(ct);
            if (!PlanRules.AllowsAgency(snapshot.PlanCode, includes, snapshot.IsTrial))
                return Result.Fail("Managing properties for other owners comes with the Agent or Enterprise plan. Change plan under Subscription to switch it on.");
        }
        // The type now only records whether the business manages for other owners (Agency); the rest follows its properties.
        business.Type = input.Type == BusinessType.Agency ? BusinessType.Agency : (business.Type == BusinessType.Developer ? BusinessType.Developer : BusinessType.Owner);
        business.Email = input.Email?.Trim();
        business.Phone = input.Phone?.Trim();
        business.PhysicalAddress = input.PhysicalAddress?.Trim();
        business.KraPin = input.KraPin?.Trim().ToUpperInvariant();
        business.ShowRentalTools = null;   // retired: menus now follow the properties (CapabilityService)
        await db.SaveChangesAsync(ct);
        capabilities?.Invalidate(business.Id);
        return Result.Ok();
    }

    public async Task<Result> SetLogoAsync(int businessId, UploadedFile? logo, CancellationToken ct = default)
    {
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.Id == businessId, ct);
        if (business is null) return Result.Fail("Business not found.");

        if (logo is null)
        {
            business.LogoPath = null;   // file kept for audit; just no longer used
        }
        else
        {
            var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) return Result.Fail("Upload the logo as a PNG or JPG image.");
            var error = UploadValidator.Validate(logo.Content, logo.FileName, logo.Length, Math.Min(2, await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct)));
            if (error is not null) return Result.Fail(error);
            business.LogoPath = await storage.SaveAsync(logo.Content, "business-logos", ext, ct);
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<int> CreateBusinessAsync(string ownerUserId, CreateBusinessRequest request, CancellationToken ct = default)
    {
        var business = new Business
        {
            Name = request.Name.Trim(),
            Type = request.Type,
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            PhysicalAddress = request.PhysicalAddress?.Trim(),
            KraPin = request.KraPin?.Trim().ToUpperInvariant()
        };
        business.Members.Add(new BusinessUser { UserId = ownerUserId, Role = BusinessRole.Owner });

        db.Businesses.Add(business);

        // FR-01-B: trial is created in the SAME SaveChanges, so a business never exists without one.
        await subscriptions.AddTrialAsync(business, ct);
        await db.SaveChangesAsync(ct);
        return business.Id;
    }
}
