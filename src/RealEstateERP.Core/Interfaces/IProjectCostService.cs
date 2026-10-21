using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// Development costing: purchase details, budget/actual cost ledger, cost allocation to units, sale margins and
/// rental yields. Depends on: DbContext, IFileStorage, ISystemConfigService, TimeProvider. Read-only on sales/leases.
/// </summary>
public interface IProjectCostService
{
    Task<IReadOnlyList<ProjectSummaryRow>> GetPortfolioAsync(CancellationToken ct = default);
    Task<ProjectOverview?> GetOverviewAsync(int assetId, CancellationToken ct = default);
    Task<IReadOnlyList<CostLine>> ListCostsAsync(int assetId, CostKind? kind, CostCategory? category, CancellationToken ct = default);
    Task<CostLine?> GetCostAsync(int costId, CancellationToken ct = default);
    Task<Result<int>> SaveCostAsync(int? costId, CostInput input, UploadedFile? receipt, CancellationToken ct = default);
    Task<Result> DeleteCostAsync(int costId, CancellationToken ct = default);
    Task<(string Key, string FileName)?> GetReceiptAsync(int costId, CancellationToken ct = default);
    Task<Result> SaveSetupAsync(int assetId, ProjectSetupInput input, CancellationToken ct = default);
    Task<Result> SetUnitTitleAsync(int unitId, string? titleNumber, CancellationToken ct = default);
}
