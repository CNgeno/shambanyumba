using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// Depends on: DbContext, INumberSequenceService, TimeProvider.
/// Owns the lease-driven unit status changes (Occupied on activation, Available on ending).
/// </summary>
public interface ILeaseService
{
    Task<IReadOnlyList<LeaseSummary>> ListAsync(LeaseListFilter filter, CancellationToken ct = default);
    Task<LeaseDetails?> GetDetailsAsync(int leaseId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaseSummary>> GetNeedingAttentionAsync(int withinDays, CancellationToken ct = default);
    Task<LeaseCounts> GetCountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LettableUnit>> GetLettableUnitsAsync(int? alsoIncludeUnitId, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(LeaseInput input, bool activate, CancellationToken ct = default);
    Task<Result> UpdateDraftAsync(int leaseId, LeaseInput input, CancellationToken ct = default);
    Task<Result> DeleteDraftAsync(int leaseId, CancellationToken ct = default);
    Task<Result> ActivateAsync(int leaseId, CancellationToken ct = default);
    Task<Result<int>> RenewAsync(int leaseId, LeaseRenewalInput input, CancellationToken ct = default);
    Task<Result> TerminateAsync(int leaseId, LeaseTerminationInput input, CancellationToken ct = default);
    Task<Result> EndAtExpiryAsync(int leaseId, bool makeUnitInactive, CancellationToken ct = default);
}
