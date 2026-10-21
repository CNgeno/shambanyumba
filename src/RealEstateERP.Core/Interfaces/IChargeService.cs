using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Charge definitions, lease charges and meter readings. Depends on: DbContext, TimeProvider.</summary>
public interface IChargeService
{
    Task<IReadOnlyList<ChargeDefinition>> ListDefinitionsAsync(bool includeInactive, CancellationToken ct = default);
    Task<ChargeDefinition?> GetDefinitionAsync(int id, CancellationToken ct = default);
    Task<Result<int>> CreateDefinitionAsync(ChargeDefinitionInput input, CancellationToken ct = default);
    Task<Result> UpdateDefinitionAsync(int id, ChargeDefinitionInput input, CancellationToken ct = default);

    Task<IReadOnlyList<LeaseChargeSummary>> GetLeaseChargesAsync(int leaseId, CancellationToken ct = default);
    Task<Result<int>> AddLeaseChargeAsync(int leaseId, LeaseChargeInput input, CancellationToken ct = default);
    Task<Result> RemoveLeaseChargeAsync(int leaseChargeId, CancellationToken ct = default);

    Task<IReadOnlyList<MeterSheetRow>> GetMeterSheetAsync(int assetId, int chargeDefinitionId, DateOnly readingDate, CancellationToken ct = default);
    Task<Result<int>> SaveReadingsAsync(int chargeDefinitionId, DateOnly readingDate, IReadOnlyList<MeterReadingEntry> entries, CancellationToken ct = default);
    Task<IReadOnlyList<MeterReadingSummary>> ListReadingsAsync(bool unbilledOnly, int? assetId, CancellationToken ct = default);
    Task<Result> DeleteReadingAsync(int readingId, CancellationToken ct = default);
}
