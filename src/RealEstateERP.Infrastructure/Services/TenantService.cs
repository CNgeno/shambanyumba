using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Depends on: DbContext, IFileStorage, ISystemConfigService, TimeProvider. Never on LeaseService.</summary>
public sealed class TenantService(
    ApplicationDbContext db,
    IFileStorage storage,
    ISystemConfigService config,
    TimeProvider clock) : ITenantService
{
    public async Task<IReadOnlyList<TenantSummary>> ListAsync(TenantListFilter filter, CancellationToken ct = default)
    {
        var query = db.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var phone = KenyanPhone.Normalize(term);
            query = query.Where(t => t.FullName.Contains(term) || (t.IdNumber != null && t.IdNumber.Contains(term))
                                  || (t.Phone != null && (t.Phone.Contains(term) || (phone != null && t.Phone == phone))));
        }

        var tenants = await query
            .OrderBy(t => t.FullName)
            .Select(t => new { t.Id, t.FullName, t.Type, t.Phone, t.IdNumber })
            .ToListAsync(ct);

        var ids = tenants.Select(t => t.Id).ToList();
        var leases = await db.Leases.AsNoTracking()
            .Where(l => ids.Contains(l.TenantId))
            .Select(l => new { l.TenantId, l.Status, l.EndDate, l.Unit.UnitNumber, l.Unit.AssetId, AssetName = l.Unit.Asset.Name })
            .ToListAsync(ct);
        var byTenant = leases.ToLookup(l => l.TenantId);

        var result = new List<TenantSummary>(tenants.Count);
        foreach (var t in tenants)
        {
            var mine = byTenant[t.Id].ToList();
            var status = LeaseRules.TenantStatusFrom(mine.Select(l => l.Status));
            var active = mine.Where(l => l.Status == LeaseStatus.Active).ToList();

            if (filter.Status is TenantStatus s && s != status) continue;
            if (filter.AssetId is int assetId && active.All(l => l.AssetId != assetId)) continue;

            result.Add(new TenantSummary(t.Id, t.FullName, t.Type, t.Phone, t.IdNumber, status,
                active.Count == 0 ? null : string.Join(", ", active.Select(l => $"{l.UnitNumber}, {l.AssetName}")),
                active.Where(l => l.EndDate != null).Select(l => l.EndDate).Min()));
        }
        return result;
    }

    public async Task<TenantDetails?> GetDetailsAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;

        var today = clock.TodayEat();
        var leases = (await db.Leases.AsNoTracking()
                .Where(l => l.TenantId == tenantId)
                .OrderByDescending(l => l.StartDate)
                .Select(l => new
                {
                    l.Id, l.LeaseNumber, l.TenantId, l.UnitId, l.Unit.UnitNumber, l.Unit.AssetId, AssetName = l.Unit.Asset.Name,
                    l.StartDate, l.EndDate, l.MonthlyRent, l.Status
                })
                .ToListAsync(ct))
            .Select(l => new LeaseSummary(l.Id, l.LeaseNumber, l.TenantId, tenant.FullName, l.UnitId, l.UnitNumber,
                l.AssetId, l.AssetName, l.StartDate, l.EndDate, l.MonthlyRent, l.Status, LeaseRules.Expiry(l.Status, l.EndDate, today)))
            .ToList();

        var documents = await db.TenantDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new TenantDocumentSummary(d.Id, d.TenantId, d.LeaseId, d.DocumentType, d.Title, d.FileName, d.SizeBytes, d.CreatedAtUtc))
            .ToListAsync(ct);

        return new TenantDetails(tenant, LeaseRules.TenantStatusFrom(leases.Select(l => l.Status)), leases, documents);
    }

    public async Task<IReadOnlyList<TenantOption>> GetOptionsAsync(CancellationToken ct = default)
        => await db.Tenants.AsNoTracking()
            .OrderBy(t => t.FullName)
            .Select(t => new TenantOption(t.Id, t.FullName, t.Phone))
            .ToListAsync(ct);

    public async Task<Result<int>> CreateAsync(TenantInput input, CancellationToken ct = default)
    {
        var (error, normalized) = await ValidateAsync(input, null, ct);
        if (error is not null) return Result<int>.Fail(error);

        var tenant = new Tenant();
        Apply(tenant, normalized!);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(tenant.Id);
    }

    public async Task<Result> UpdateAsync(int tenantId, TenantInput input, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Result.Fail("Tenant not found.");

        var (error, normalized) = await ValidateAsync(input, tenantId, ct);
        if (error is not null) return Result.Fail(error);

        Apply(tenant, normalized!);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Result.Fail("Tenant not found.");
        if (await db.Leases.AnyAsync(l => l.TenantId == tenantId, ct))
            return Result.Fail($"{tenant.FullName} has lease history, so the record is kept for your accounts.");

        var docs = await db.TenantDocuments.Where(d => d.TenantId == tenantId).ToListAsync(ct);
        db.TenantDocuments.RemoveRange(docs);
        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> UploadDocumentAsync(int tenantId, int? leaseId, TenantDocumentType type, string? title,
        UploadedFile file, CancellationToken ct = default)
    {
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, ct))
            return Result<int>.Fail("Tenant not found.");
        if (leaseId is int lid && !await db.Leases.AnyAsync(l => l.Id == lid && l.TenantId == tenantId, ct))
            return Result<int>.Fail("That lease doesn't belong to this tenant.");

        var maxMb = await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct);
        var error = UploadValidator.Validate(file.Content, file.FileName, file.Length, maxMb);
        if (error is not null) return Result<int>.Fail(error);

        var key = await storage.SaveAsync(file.Content, "tenant-documents", Path.GetExtension(file.FileName), ct);
        var doc = new TenantDocument
        {
            TenantId = tenantId, LeaseId = leaseId, DocumentType = type, Title = title?.Trim(),
            FileName = Path.GetFileName(file.FileName), StorageKey = key, SizeBytes = file.Length
        };
        db.TenantDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(doc.Id);
    }

    public Task<TenantDocument?> GetDocumentAsync(int documentId, CancellationToken ct = default)
        => db.TenantDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct);

    public async Task<Result> DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var doc = await db.TenantDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result.Fail("Document not found.");
        db.TenantDocuments.Remove(doc);   // soft delete; the file is kept for audit
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------------

    private async Task<(string? Error, TenantInput? Normalized)> ValidateAsync(TenantInput input, int? tenantId, CancellationToken ct)
    {
        var name = input.FullName.Trim();
        if (name.Length == 0) return ("Enter the tenant's name.", null);

        string? phone = null, alt = null, emergency = null;
        if (!string.IsNullOrWhiteSpace(input.Phone) && (phone = KenyanPhone.Normalize(input.Phone)) is null)
            return ("Enter a valid phone number, for example 0712 345 678.", null);
        if (!string.IsNullOrWhiteSpace(input.AltPhone) && (alt = KenyanPhone.Normalize(input.AltPhone)) is null)
            return ("The alternative phone number isn't valid.", null);
        if (!string.IsNullOrWhiteSpace(input.EmergencyContactPhone) && (emergency = KenyanPhone.Normalize(input.EmergencyContactPhone)) is null)
            return ("The emergency contact's phone number isn't valid.", null);

        var idNumber = string.IsNullOrWhiteSpace(input.IdNumber) ? null : input.IdNumber.Trim().ToUpperInvariant();
        if (idNumber is not null && await db.Tenants.AnyAsync(t => t.IdNumber == idNumber && t.Id != (tenantId ?? 0), ct))
            return ($"Another tenant already has ID number {idNumber}.", null);

        return (null, input with
        {
            FullName = name,
            IdNumber = idNumber,
            IdType = idNumber is null ? null : input.IdType,
            KraPin = string.IsNullOrWhiteSpace(input.KraPin) ? null : input.KraPin.Trim().ToUpperInvariant(),
            Phone = phone,
            AltPhone = alt,
            EmergencyContactPhone = emergency,
            Email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim().ToLowerInvariant()
        });
    }

    private static void Apply(Tenant t, TenantInput i)
    {
        t.Type = i.Type;
        t.FullName = i.FullName;
        t.IdType = i.IdType;
        t.IdNumber = i.IdNumber;
        t.KraPin = i.KraPin;
        t.Phone = i.Phone;
        t.AltPhone = i.AltPhone;
        t.Email = i.Email;
        t.PostalAddress = i.PostalAddress?.Trim();
        t.Employer = i.Employer?.Trim();
        t.EmergencyContactName = i.EmergencyContactName?.Trim();
        t.EmergencyContactPhone = i.EmergencyContactPhone;
        t.EmergencyContactRelationship = i.EmergencyContactRelationship?.Trim();
        t.Notes = i.Notes?.Trim();
    }
}
