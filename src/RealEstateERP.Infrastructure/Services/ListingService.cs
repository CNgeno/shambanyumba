using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>FR-12 business side. Depends on: DbContext, IFileStorage, ISystemConfigService, ICurrentUserService, TimeProvider.</summary>
public sealed class ListingService(
    ApplicationDbContext db,
    IFileStorage storage,
    ISystemConfigService config,
    ICurrentUserService currentUser,
    TimeProvider clock) : IListingService
{
    private int BusinessId => currentUser.BusinessId ?? throw new InvalidOperationException("No business selected.");
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ------------------------------------------------------------------ listings

    public async Task<IReadOnlyList<ListingSummary>> ListAsync(ListingStatus? status, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var query = db.Listings.AsNoTracking();
        if (status is ListingStatus s) query = query.Where(l => l.Status == s);
        else query = query.Where(l => l.Status != ListingStatus.Closed);
        return await query.OrderByDescending(l => l.Status == ListingStatus.Published).ThenByDescending(l => l.Id)
            .Select(l => new ListingSummary(l.Id, l.Type, l.Title, l.Price, l.Status, l.UnitId, l.Unit.UnitNumber, l.Unit.Status, l.Unit.AssetId,
                l.Unit.Asset.Name, l.Photos.Count, l.ViewCount,
                db.ListingLeads.Count(x => x.BusinessId == businessId && x.ListingId == l.Id && x.Status != LeadStatus.Unverified),
                l.PublishedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<ListingDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var listing = await db.Listings.AsNoTracking().Include(l => l.Unit).ThenInclude(u => u.Asset).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return null;
        var photos = await db.ListingPhotos.AsNoTracking().Where(p => p.ListingId == id).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        var leads = await db.ListingLeads.CountAsync(x => x.BusinessId == businessId && x.ListingId == id && x.Status != LeadStatus.Unverified, ct);
        return new ListingDetails(listing, photos, leads);
    }

    public async Task<IReadOnlyList<ListableUnit>> GetListableUnitsAsync(CancellationToken ct = default)
    {
        var units = await db.Units.AsNoTracking().Where(u => u.Status != UnitStatus.Inactive && u.Status != UnitStatus.Sold)
            .Select(u => new { u.Id, u.UnitNumber, AssetName = u.Asset.Name, u.Asset.Location, u.Type, u.Bedrooms, u.SizeSqm, u.MonthlyRent, u.SalePrice, u.Status })
            .ToListAsync(ct);
        return units
            .OrderBy(u => u.Status == UnitStatus.Available ? 0 : 1).ThenBy(u => u.AssetName).ThenBy(u => u.UnitNumber, NaturalComparer.Instance)
            .Select(u => new ListableUnit(u.Id, u.UnitNumber, u.AssetName, u.Type, u.MonthlyRent, u.SalePrice,
                MarketplaceRules.SuggestTitle(ListingType.Rent, u.Type, u.Bedrooms, u.SizeSqm, u.Location ?? u.AssetName),
                MarketplaceRules.SuggestTitle(ListingType.Sale, u.Type, u.Bedrooms, u.SizeSqm, u.Location ?? u.AssetName)))
            .ToList();
    }

    public async Task<Result<int>> CreateAsync(ListingInput input, CancellationToken ct = default)
    {
        var error = MarketplaceRules.ValidateListing(input.Title, input.Description, input.Price);
        if (error is not null) return Result<int>.Fail(error);
        if (!await db.Units.AnyAsync(u => u.Id == input.UnitId && u.Status != UnitStatus.Inactive, ct)) return Result<int>.Fail("Choose the unit to advertise.");
        if (await db.Listings.AnyAsync(l => l.UnitId == input.UnitId && l.Type == input.Type && l.Status != ListingStatus.Closed, ct))
            return Result<int>.Fail($"This unit already has an open {(input.Type == ListingType.Rent ? "rental" : "sale")} listing.");

        var listing = new Listing { UnitId = input.UnitId, Type = input.Type, Title = input.Title.Trim(), Description = input.Description.Trim(), Price = input.Price };
        db.Listings.Add(listing);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result<int>.Fail("This unit already has an open listing of that type."); }
        return Result<int>.Ok(listing.Id);
    }

    public async Task<Result> UpdateAsync(int id, ListingInput input, CancellationToken ct = default)
    {
        var error = MarketplaceRules.ValidateListing(input.Title, input.Description, input.Price);
        if (error is not null) return Result.Fail(error);
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return Result.Fail("Listing not found.");
        if (listing.Status == ListingStatus.Closed) return Result.Fail("A closed listing can't be changed. Create a new one.");
        listing.Title = input.Title.Trim();
        listing.Description = input.Description.Trim();
        listing.Price = input.Price;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddPhotosAsync(int id, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default)
    {
        var listing = await db.Listings.Include(l => l.Photos).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return Result.Fail("Listing not found.");
        if (photos.Count == 0) return Result.Fail("Choose at least one photo.");
        if (listing.Photos.Count + photos.Count > MarketplaceRules.MaxPhotos) return Result.Fail($"A listing can have up to {MarketplaceRules.MaxPhotos} photos.");

        var maxMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct);
        var order = listing.Photos.Count == 0 ? 0 : listing.Photos.Max(p => p.SortOrder) + 1;
        foreach (var photo in photos)
        {
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png")) return Result.Fail($"{photo.FileName}: photos must be JPG or PNG.");
            var error = UploadValidator.Validate(photo.Content, photo.FileName, photo.Length, maxMb);
            if (error is not null) return Result.Fail($"{photo.FileName}: {error}");
        }
        foreach (var photo in photos)
        {
            photo.Content.Position = 0;
            var key = await storage.SaveAsync(photo.Content, "listing-photos", Path.GetExtension(photo.FileName).ToLowerInvariant(), ct);
            listing.Photos.Add(new ListingPhoto { StorageKey = key, FileName = Path.GetFileName(photo.FileName), SortOrder = order++ });
        }
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemovePhotoAsync(int id, int photoId, CancellationToken ct = default)
    {
        var photo = await db.ListingPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.ListingId == id, ct);
        if (photo is null) return Result.Fail("Photo not found.");
        db.ListingPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MakeCoverAsync(int id, int photoId, CancellationToken ct = default)
    {
        var photos = await db.ListingPhotos.Where(p => p.ListingId == id).OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync(ct);
        var cover = photos.FirstOrDefault(p => p.Id == photoId);
        if (cover is null) return Result.Fail("Photo not found.");
        var order = 1;
        foreach (var p in photos) p.SortOrder = p.Id == photoId ? 0 : order++;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetStatusAsync(int id, ListingStatus status, CancellationToken ct = default)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return Result.Fail("Listing not found.");
        if (listing.Status == ListingStatus.Closed) return Result.Fail("This listing is closed. Create a new one to advertise again.");
        if (status == ListingStatus.Published)
        {
            var error = MarketplaceRules.ValidateListing(listing.Title, listing.Description, listing.Price);
            if (error is not null) return Result.Fail(error);
            listing.PublishedAtUtc ??= Now;
        }
        if (status == ListingStatus.Closed) listing.ClosedAtUtc = Now;
        listing.Status = status;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public Task<ListingPhoto?> GetPhotoAsync(int photoId, CancellationToken ct = default)
        => db.ListingPhotos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == photoId, ct);

    // ------------------------------------------------------------------ leads

    public async Task<LeadsOverview> GetLeadsAsync(LeadStage? stage, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var query = db.ListingLeads.AsNoTracking().Where(l => l.BusinessId == businessId && l.Status != LeadStatus.Unverified);
        if (stage is LeadStage s) query = query.Where(l => l.Stage == s);
        var rows = await query.OrderByDescending(l => l.CreatedAtUtc).Take(500)
            .Select(l => new
            {
                l.Id, l.ListingId, ListingTitle = l.Listing.Title, UnitNumber = l.Listing.Unit.UnitNumber, l.CreatedAtUtc, l.Status, l.Stage, l.IsRepeat,
                l.CreditsCharged, l.UnlockedAtUtc, l.Name, l.Phone, l.Email, l.Message, l.Notes
            })
            .ToListAsync(ct);

        var leads = rows.Select(r =>
        {
            var visible = r.Status is LeadStatus.Unlocked or LeadStatus.Refunded;
            return new LeadRow(r.Id, r.ListingId, r.ListingTitle, r.UnitNumber, r.CreatedAtUtc, r.Status, r.Stage, r.IsRepeat, r.CreditsCharged, r.UnlockedAtUtc,
                visible ? r.Name : FirstNameOnly(r.Name), visible ? r.Phone : null, visible ? r.Email : null, visible ? r.Message : null, r.Notes);
        }).ToList();

        var balance = await db.Businesses.Where(b => b.Id == businessId).Select(b => b.CreditBalance).FirstAsync(ct);
        return new LeadsOverview(leads, leads.Count(l => l.Status == LeadStatus.AwaitingCredits),
            await config.GetIntAsync(ConfigKeys.LeadCredits, 10, ct), balance, await config.GetIntAsync(ConfigKeys.LeadRefundDays, 7, ct));
    }

    public async Task<Result> UnlockAsync(int leadId, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var lead = await db.ListingLeads.Include(l => l.Listing).FirstOrDefaultAsync(l => l.Id == leadId && l.BusinessId == businessId, ct);
        if (lead is null) return Result.Fail("Lead not found.");
        if (lead.Status != LeadStatus.AwaitingCredits) return Result.Fail("This lead is already open.");

        var cost = await config.GetIntAsync(ConfigKeys.LeadCredits, 10, ct);
        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, ct);
        if (business.CreditBalance < cost) return Result.Fail($"Opening a lead uses {cost} credits and you have {business.CreditBalance}. Buy credits first.");

        db.CreditTransactions.Add(CreditLedger.Post(business, -cost, CreditTransactionType.Usage, $"Lead: {lead.Listing.Title}", $"LEAD-{lead.Id}"));
        lead.Status = LeadStatus.Unlocked;
        lead.CreditsCharged = cost;
        lead.UnlockedAtUtc = Now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result.Fail("Your credits changed at the same moment. Try again."); }
        return Result.Ok();
    }

    public async Task<Result> UpdateLeadAsync(int leadId, LeadStage stage, string? notes, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var lead = await db.ListingLeads.FirstOrDefaultAsync(l => l.Id == leadId && l.BusinessId == businessId, ct);
        if (lead is null || lead.Status is LeadStatus.Unverified) return Result.Fail("Lead not found.");
        if (lead.Status == LeadStatus.AwaitingCredits) return Result.Fail("Open the lead first.");
        lead.Stage = stage;
        lead.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RefundAsync(int leadId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Say what was wrong with the lead, e.g. wrong number.");
        var businessId = BusinessId;
        var lead = await db.ListingLeads.FirstOrDefaultAsync(l => l.Id == leadId && l.BusinessId == businessId, ct);
        if (lead is null) return Result.Fail("Lead not found.");

        var now = Now;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var refundsThisMonth = await db.ListingLeads.CountAsync(l => l.BusinessId == businessId && l.RefundedAtUtc >= monthStart, ct);
        var error = MarketplaceRules.RefundError(lead.Status, lead.UnlockedAtUtc, lead.IsRepeat, now,
            await config.GetIntAsync(ConfigKeys.LeadRefundDays, 7, ct), refundsThisMonth, await config.GetIntAsync(ConfigKeys.LeadMaxRefundsPerMonth, 5, ct));
        if (error is not null) return Result.Fail(error);

        var business = await db.Businesses.FirstAsync(b => b.Id == businessId, ct);
        db.CreditTransactions.Add(CreditLedger.Post(business, lead.CreditsCharged, CreditTransactionType.Adjustment, $"Lead refund: {reason.Trim()}", $"LEAD-{lead.Id}"));
        lead.Status = LeadStatus.Refunded;
        lead.RefundedAtUtc = now;
        lead.RefundReason = reason.Trim();
        lead.Stage = LeadStage.Lost;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<int> AutoListVacantAsync(CancellationToken ct = default)
    {
        // Runs for the current business (the daily job opens a business scope); tenant filters apply here.
        var units = await db.Units.AsNoTracking()
            .Where(u => u.Status == UnitStatus.Available && u.MonthlyRent > 0 && u.SalePrice == null
                        && !db.Listings.Any(l => l.UnitId == u.Id && l.Type == ListingType.Rent && l.Status != ListingStatus.Closed))
            .Select(u => new { u.Id, u.Type, u.Bedrooms, u.SizeSqm, u.MonthlyRent, u.AssetId, AssetName = u.Asset.Name, u.Asset.Location, u.Asset.Description })
            .ToListAsync(ct);
        if (units.Count == 0) return 0;

        var unitIds = units.Select(u => u.Id).ToList();
        var assetIds = units.Select(u => u.AssetId).Distinct().ToList();
        var interiors = await db.UnitPhotos.AsNoTracking().Where(p => unitIds.Contains(p.UnitId)).OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new { p.UnitId, p.StorageKey, p.FileName }).ToListAsync(ct);
        var estatePhotos = await db.AssetPhotos.AsNoTracking().Where(p => assetIds.Contains(p.AssetId)).OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new { p.AssetId, p.StorageKey, p.FileName }).ToListAsync(ct);

        foreach (var u in units)
        {
            var title = MarketplaceRules.SuggestTitle(ListingType.Rent, u.Type, u.Bedrooms, u.SizeSqm, u.Location ?? u.AssetName);
            var description = !string.IsNullOrWhiteSpace(u.Description) && u.Description.Trim().Length >= 40
                ? u.Description.Trim()
                : $"{title} at {u.AssetName}. Rent KES {u.MonthlyRent:N0} a month. Send an enquiry to arrange a viewing.";
            var listing = new Listing
            {
                UnitId = u.Id, Type = ListingType.Rent, Title = title.Length > 120 ? title[..120] : title,
                Description = description.Length > 2000 ? description[..2000] : description,
                Price = u.MonthlyRent, Status = ListingStatus.Published, PublishedAtUtc = Now
            };
            db.Listings.Add(listing);
            // Interiors first, then the estate's photos; the same stored files are reused (nothing is copied).
            var photos = interiors.Where(p => p.UnitId == u.Id).Select(p => (p.StorageKey, p.FileName))
                .Concat(estatePhotos.Where(p => p.AssetId == u.AssetId).Select(p => (p.StorageKey, p.FileName)))
                .Take(12).ToList();
            for (var i = 0; i < photos.Count; i++)
                db.ListingPhotos.Add(new ListingPhoto { Listing = listing, StorageKey = photos[i].StorageKey, FileName = photos[i].FileName, SortOrder = i });
        }
        await db.SaveChangesAsync(ct);
        return units.Count;
    }

    public async Task<IReadOnlyList<PendingEnquiry>> GetAwaitingConfirmationAsync(int? assetId, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var rows = await db.ListingLeads.AsNoTracking()
            .Where(l => l.BusinessId == businessId && l.Status == LeadStatus.Unverified && l.VerifyBeforeUtc > Now
                        && (assetId == null || l.Listing.Unit.AssetId == assetId))
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new { l.ListingId, l.Listing.Title, l.Name, l.CreatedAtUtc, l.VerifyBeforeUtc })
            .Take(50).ToListAsync(ct);
        return rows.Select(r => new PendingEnquiry(r.ListingId, r.Title, r.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Someone",
            r.CreatedAtUtc, r.VerifyBeforeUtc)).ToList();
    }

    public async Task<PropertyEnquiries> GetPropertyEnquiriesAsync(int assetId, CancellationToken ct = default)
    {
        var businessId = BusinessId;
        var live = await db.Listings.CountAsync(l => l.Unit.AssetId == assetId && l.Status == ListingStatus.Published && l.Unit.Status == UnitStatus.Available, ct);
        var leads = db.ListingLeads.AsNoTracking().Where(l => l.BusinessId == businessId && l.Listing.Unit.AssetId == assetId);
        var newLeads = await leads.CountAsync(l => l.Status == LeadStatus.AwaitingCredits || (l.Status == LeadStatus.Unlocked && l.Stage == LeadStage.New), ct);
        var total = await leads.CountAsync(l => l.Status == LeadStatus.Unlocked || l.Status == LeadStatus.AwaitingCredits, ct);
        var waiting = await leads.CountAsync(l => l.Status == LeadStatus.Unverified && l.VerifyBeforeUtc > Now, ct);
        return new PropertyEnquiries(live, newLeads, total, waiting);
    }

    public async Task<int> CountNewLeadsAsync(CancellationToken ct = default)
    {
        var businessId = BusinessId;
        return await db.ListingLeads.CountAsync(l => l.BusinessId == businessId
            && (l.Status == LeadStatus.AwaitingCredits || (l.Status == LeadStatus.Unlocked && l.Stage == LeadStage.New)), ct);
    }

    private static string FirstNameOnly(string name) => (name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Someone");
}
