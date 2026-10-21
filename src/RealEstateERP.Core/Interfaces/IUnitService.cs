using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Units. Depends on the DbContext and ISubscriptionService (for the plan unit limit).</summary>
public interface IUnitService
{
    Task<Unit?> GetAsync(int unitId, CancellationToken ct = default);
    Task<UnitUsage> GetUsageAsync(CancellationToken ct = default);

    Task<Result<int>> CreateAsync(UnitInput input, CancellationToken ct = default);
    Task<Result<int>> BulkCreateAsync(BulkUnitInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int unitId, UnitInput input, CancellationToken ct = default);
    Task<Result> ChangeStatusAsync(int unitId, UnitStatus status, CancellationToken ct = default);
    Task<Result> DeleteAsync(int unitId, CancellationToken ct = default);
}
