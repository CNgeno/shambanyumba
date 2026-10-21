using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Assets and their structure trees. Depends only on the DbContext.</summary>
public interface IAssetService
{
    Task<IReadOnlyList<AssetSummary>> ListAsync(CancellationToken ct = default);
    Task<AssetDetails?> GetDetailsAsync(int assetId, CancellationToken ct = default);
    Task<PortfolioTotals> GetTotalsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StructureTreeRow>> GetTreeAsync(int assetId, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(AssetInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int assetId, AssetInput input, CancellationToken ct = default);
    Task<Result> DeleteAsync(int assetId, CancellationToken ct = default);

    Task<Result<int>> AddNodeAsync(int assetId, StructureNodeInput input, CancellationToken ct = default);
    Task<Result> UpdateNodeAsync(int nodeId, StructureNodeInput input, CancellationToken ct = default);
    Task<Result> DeleteNodeAsync(int nodeId, CancellationToken ct = default);
}
