using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// An estate's setup: its charges, photos (estate and house interiors), description and guided setup steps.
/// Depends on: DbContext, IFileStorage, ISystemConfigService, TimeProvider.
/// </summary>
public interface IEstateService
{
    Task<IReadOnlyList<PropertyChargeRow>> GetChargesAsync(int assetId, CancellationToken ct = default);
    Task<Result> AddChargeAsync(int assetId, PropertyChargeInput input, CancellationToken ct = default);
    Task<Result> RemoveChargeAsync(int assetId, int propertyChargeId, CancellationToken ct = default);
    /// <summary>Adds the estate's charges to every active or draft lease in the estate that doesn't have them. Returns how many were added.</summary>
    Task<Result<int>> ApplyChargesToLeasesAsync(int assetId, CancellationToken ct = default);

    Task<IReadOnlyList<PhotoRow>> GetEstatePhotosAsync(int assetId, CancellationToken ct = default);
    Task<Result> AddEstatePhotosAsync(int assetId, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default);
    Task<Result> RemoveEstatePhotoAsync(int assetId, int photoId, CancellationToken ct = default);
    Task<IReadOnlyList<PhotoRow>> GetUnitPhotosAsync(int unitId, CancellationToken ct = default);
    Task<Result> AddUnitPhotosAsync(int unitId, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default);
    Task<Result> RemoveUnitPhotoAsync(int unitId, int photoId, CancellationToken ct = default);
    /// <summary>Storage key of an estate ("estate") or interior ("unit") photo of the current business.</summary>
    Task<string?> GetPhotoKeyAsync(string kind, int photoId, CancellationToken ct = default);

    Task<Result> SaveDescriptionAsync(int assetId, string? description, CancellationToken ct = default);
    Task<EstateSetup> GetSetupAsync(int assetId, CancellationToken ct = default);
}
