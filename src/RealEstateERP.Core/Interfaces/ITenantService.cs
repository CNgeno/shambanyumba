using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Depends on: DbContext, IFileStorage, ISystemConfigService.</summary>
public interface ITenantService
{
    Task<IReadOnlyList<TenantSummary>> ListAsync(TenantListFilter filter, CancellationToken ct = default);
    Task<TenantDetails?> GetDetailsAsync(int tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TenantOption>> GetOptionsAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(TenantInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int tenantId, TenantInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int tenantId, CancellationToken ct = default);

    Task<Result<int>> UploadDocumentAsync(int tenantId, int? leaseId, TenantDocumentType type, string? title, UploadedFile file, CancellationToken ct = default);
    Task<TenantDocument?> GetDocumentAsync(int documentId, CancellationToken ct = default);
    Task<Result> DeleteDocumentAsync(int documentId, CancellationToken ct = default);
}
