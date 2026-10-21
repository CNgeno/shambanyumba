using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Estate setup: charges, photos, description and the guided checklist. Depends on: DbContext, IFileStorage, ISystemConfigService.</summary>
public sealed class EstateService(ApplicationDbContext db, IFileStorage storage, ISystemConfigService config) : IEstateService
{
    public const int MaxPhotos = 12;

    // ------------------------------------------------------------------ charges

    public async Task<IReadOnlyList<PropertyChargeRow>> GetChargesAsync(int assetId, CancellationToken ct = default)
    {
        var charges = await db.PropertyCharges.AsNoTracking().Where(p => p.AssetId == assetId)
            .Select(p => new { p.Id, p.ChargeDefinitionId, p.ChargeDefinition.Name, p.ChargeDefinition.Type, p.ChargeDefinition.Frequency,
                Amount = p.Amount ?? p.ChargeDefinition.DefaultAmount, Rate = p.UnitRate ?? p.ChargeDefinition.UnitRate, p.ChargeDefinition.UnitOfMeasure })
            .OrderBy(p => p.Name).ToListAsync(ct);
        var leases = await OpenLeasesAsync(assetId, ct);
        var leaseIds = leases.Select(l => l.Id).ToList();
        var onLeases = await db.LeaseCharges.AsNoTracking().Where(c => leaseIds.Contains(c.LeaseId))
            .Select(c => new { c.LeaseId, c.ChargeDefinitionId }).ToListAsync(ct);
        return charges.Select(c => new PropertyChargeRow(c.Id, c.ChargeDefinitionId, c.Name, c.Type, c.Frequency, c.Amount, c.Rate, c.UnitOfMeasure,
            leaseIds.Count(id => !onLeases.Any(o => o.LeaseId == id && o.ChargeDefinitionId == c.ChargeDefinitionId)))).ToList();
    }

    public async Task<Result> AddChargeAsync(int assetId, PropertyChargeInput input, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct)) return Result.Fail("Property not found.");
        var def = await db.ChargeDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == input.ChargeDefinitionId && d.IsActive, ct);
        if (def is null) return Result.Fail("Choose a charge.");
        if (def.Type == ChargeType.OneTime) return Result.Fail("One-off charges are added to individual invoices, not to an estate.");
        if (input.Amount is < 0 || input.UnitRate is < 0) return Result.Fail("Amounts can't be negative.");
        if (await db.PropertyCharges.AnyAsync(p => p.AssetId == assetId && p.ChargeDefinitionId == def.Id, ct)) return Result.Fail($"{def.Name} is already an estate charge here.");

        db.PropertyCharges.Add(new PropertyCharge
        {
            AssetId = assetId, ChargeDefinitionId = def.Id,
            Amount = def.Type == ChargeType.Recurring && input.Amount != def.DefaultAmount ? input.Amount : null,
            UnitRate = def.Type == ChargeType.Utility && input.UnitRate != def.UnitRate ? input.UnitRate : null
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveChargeAsync(int assetId, int propertyChargeId, CancellationToken ct = default)
    {
        var charge = await db.PropertyCharges.FirstOrDefaultAsync(p => p.Id == propertyChargeId && p.AssetId == assetId, ct);
        if (charge is null) return Result.Fail("Charge not found.");
        db.PropertyCharges.Remove(charge);   // leases keep it until removed from each lease
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> ApplyChargesToLeasesAsync(int assetId, CancellationToken ct = default)
    {
        var charges = await db.PropertyCharges.AsNoTracking().Where(p => p.AssetId == assetId).ToListAsync(ct);
        if (charges.Count == 0) return Result<int>.Fail("Add the estate's charges first.");
        var leaseIds = (await OpenLeasesAsync(assetId, ct)).Select(l => l.Id).ToList();
        var existing = await db.LeaseCharges.AsNoTracking().Where(c => leaseIds.Contains(c.LeaseId))
            .Select(c => new { c.LeaseId, c.ChargeDefinitionId }).ToListAsync(ct);

        var added = 0;
        foreach (var leaseId in leaseIds)
            foreach (var c in charges.Where(c => !existing.Any(e => e.LeaseId == leaseId && e.ChargeDefinitionId == c.ChargeDefinitionId)))
            {
                db.LeaseCharges.Add(new LeaseCharge { LeaseId = leaseId, ChargeDefinitionId = c.ChargeDefinitionId, AmountOverride = c.Amount, UnitRateOverride = c.UnitRate });
                added++;
            }
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(added);
    }

    /// <summary>For LeaseService: the estate's charges as lease charges, ready to attach to a new lease.</summary>
    public static async Task<List<LeaseCharge>> ChargesForNewLeaseAsync(ApplicationDbContext db, int unitId, CancellationToken ct)
        => await db.PropertyCharges.AsNoTracking()
            .Where(p => db.Units.Any(u => u.Id == unitId && u.AssetId == p.AssetId))
            .Select(p => new LeaseCharge { ChargeDefinitionId = p.ChargeDefinitionId, AmountOverride = p.Amount, UnitRateOverride = p.UnitRate })
            .ToListAsync(ct);

    // ------------------------------------------------------------------ photos

    public async Task<IReadOnlyList<PhotoRow>> GetEstatePhotosAsync(int assetId, CancellationToken ct = default)
        => await db.AssetPhotos.AsNoTracking().Where(p => p.AssetId == assetId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new PhotoRow(p.Id, p.FileName, p.Caption)).ToListAsync(ct);

    public async Task<Result> AddEstatePhotosAsync(int assetId, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct)) return Result.Fail("Property not found.");
        var count = await db.AssetPhotos.CountAsync(p => p.AssetId == assetId, ct);
        var saved = await SavePhotosAsync(photos, count, "estate-photos", ct);
        if (!saved.Succeeded) return Result.Fail(saved.Error!);
        var order = count;
        foreach (var (key, name) in saved.Value!) db.AssetPhotos.Add(new AssetPhoto { AssetId = assetId, StorageKey = key, FileName = name, SortOrder = order++ });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveEstatePhotoAsync(int assetId, int photoId, CancellationToken ct = default)
    {
        var photo = await db.AssetPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.AssetId == assetId, ct);
        if (photo is null) return Result.Fail("Photo not found.");
        db.AssetPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<PhotoRow>> GetUnitPhotosAsync(int unitId, CancellationToken ct = default)
        => await db.UnitPhotos.AsNoTracking().Where(p => p.UnitId == unitId).OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new PhotoRow(p.Id, p.FileName, p.Caption)).ToListAsync(ct);

    public async Task<Result> AddUnitPhotosAsync(int unitId, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default)
    {
        if (!await db.Units.AnyAsync(u => u.Id == unitId, ct)) return Result.Fail("Unit not found.");
        var count = await db.UnitPhotos.CountAsync(p => p.UnitId == unitId, ct);
        var saved = await SavePhotosAsync(photos, count, "unit-photos", ct);
        if (!saved.Succeeded) return Result.Fail(saved.Error!);
        var order = count;
        foreach (var (key, name) in saved.Value!) db.UnitPhotos.Add(new UnitPhoto { UnitId = unitId, StorageKey = key, FileName = name, SortOrder = order++ });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveUnitPhotoAsync(int unitId, int photoId, CancellationToken ct = default)
    {
        var photo = await db.UnitPhotos.FirstOrDefaultAsync(p => p.Id == photoId && p.UnitId == unitId, ct);
        if (photo is null) return Result.Fail("Photo not found.");
        db.UnitPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<string?> GetPhotoKeyAsync(string kind, int photoId, CancellationToken ct = default)
        => kind == "unit"
            ? await db.UnitPhotos.AsNoTracking().Where(p => p.Id == photoId).Select(p => p.StorageKey).FirstOrDefaultAsync(ct)
            : await db.AssetPhotos.AsNoTracking().Where(p => p.Id == photoId).Select(p => p.StorageKey).FirstOrDefaultAsync(ct);

    // ------------------------------------------------------------------ description and setup

    public async Task<Result> SaveDescriptionAsync(int assetId, string? description, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Property not found.");
        if (description?.Length > 1000) return Result.Fail("Keep the description under 1,000 characters.");
        asset.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<EstateSetup> GetSetupAsync(int assetId, CancellationToken ct = default)
    {
        var unitIds = await db.Units.AsNoTracking().Where(u => u.AssetId == assetId && u.Status != UnitStatus.Inactive).Select(u => u.Id).ToListAsync(ct);
        var photos = await db.AssetPhotos.CountAsync(p => p.AssetId == assetId, ct);
        var charges = await db.PropertyCharges.AsNoTracking().Where(p => p.AssetId == assetId)
            .Select(p => new { p.ChargeDefinitionId, p.ChargeDefinition.Type }).ToListAsync(ct);
        var metered = charges.Where(c => c.Type == ChargeType.Utility).Select(c => c.ChargeDefinitionId).ToList();
        var readingsDone = metered.Count == 0 || unitIds.Count == 0 || await AllMetersStartedAsync(unitIds, metered, ct);
        var leases = await db.Leases.CountAsync(l => unitIds.Contains(l.UnitId) && l.Status == LeaseStatus.Active, ct);
        var invoiced = await db.Invoices.AnyAsync(i => i.Lease != null && unitIds.Contains(i.Lease.UnitId) && i.Status != InvoiceStatus.Cancelled, ct);
        var listed = await db.Listings.AnyAsync(l => unitIds.Contains(l.UnitId) && l.Status != ListingStatus.Closed, ct);

        var steps = new List<SetupStepStatus>
        {
            new("units", "Add the units", "Each house or unit with its rent.", unitIds.Count > 0, false),
            new("photos", "Add photos of the estate", "Gate, compound, exterior; interiors per unit are optional. Used on listings.", photos > 0, true),
            new("charges", "Set the estate's charges", "Garbage, security, water and so on, with this estate's amounts. New leases get them automatically.", charges.Count > 0, false)
        };
        if (metered.Count > 0)
            steps.Add(new("readings", "Enter opening meter readings", "One starting reading per unit for each metered charge.", readingsDone, false));
        steps.Add(new("leases", "Add tenants and leases", "The estate's charges are added to each new lease.", leases > 0, false));
        steps.Add(new("invoices", "Send the first invoices", "The preview warns about any missing charge or reading.", invoiced, false));
        steps.Add(new("listing", "Advertise vacant units", "List them yourself, or switch on automatic listing in Business settings.", listed, true));
        return new EstateSetup(steps);
    }

    // ---------------------------------------------------------------------

    private async Task<bool> AllMetersStartedAsync(List<int> unitIds, List<int> metered, CancellationToken ct)
    {
        var started = await db.UtilityMeterReadings.AsNoTracking()
            .Where(r => unitIds.Contains(r.UnitId) && metered.Contains(r.ChargeDefinitionId))
            .Select(r => new { r.UnitId, r.ChargeDefinitionId }).Distinct().CountAsync(ct);
        return started >= unitIds.Count * metered.Count;
    }

    private Task<List<Lease>> OpenLeasesAsync(int assetId, CancellationToken ct)
        => db.Leases.AsNoTracking().Where(l => l.Unit.AssetId == assetId && (l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Draft)).ToListAsync(ct);

    private async Task<Result<List<(string Key, string Name)>>> SavePhotosAsync(IReadOnlyList<UploadedFile> photos, int existing, string folder, CancellationToken ct)
    {
        if (photos.Count == 0) return Result<List<(string, string)>>.Fail("Choose at least one photo.");
        if (existing + photos.Count > MaxPhotos) return Result<List<(string, string)>>.Fail($"Up to {MaxPhotos} photos each.");
        var maxMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct);
        foreach (var photo in photos)
        {
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png")) return Result<List<(string, string)>>.Fail($"{photo.FileName}: photos must be JPG or PNG.");
            var error = UploadValidator.Validate(photo.Content, photo.FileName, photo.Length, maxMb);
            if (error is not null) return Result<List<(string, string)>>.Fail($"{photo.FileName}: {error}");
        }
        var saved = new List<(string, string)>();
        foreach (var photo in photos)
        {
            photo.Content.Position = 0;
            saved.Add((await storage.SaveAsync(photo.Content, folder, Path.GetExtension(photo.FileName).ToLowerInvariant(), ct), Path.GetFileName(photo.FileName)));
        }
        return Result<List<(string, string)>>.Ok(saved);
    }
}
