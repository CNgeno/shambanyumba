using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Depends on: DbContext only. Every query is tenant-filtered automatically.
/// RULE: look rows up with FirstOrDefaultAsync(x => x.Id == id), never FindAsync, so query filters always apply.
/// </summary>
public sealed class AssetService(ApplicationDbContext db, ICapabilityService? capabilities = null) : IAssetService
{
    /// <summary>Properties decide the menus (to let / for sale), so refresh the business's capabilities after a change.</summary>
    private async Task SaveAndRefreshAsync(Asset asset, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        capabilities?.Invalidate(asset.BusinessId);
    }

    public async Task<IReadOnlyList<AssetSummary>> ListAsync(CancellationToken ct = default)
    {
        var assets = await db.Assets.AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.Type, a.LifecycleState, a.Location, a.County, a.Purpose })
            .ToListAsync(ct);

        var stats = await LoadStatsAsync(null, ct);
        return assets
            .Select(a => new AssetSummary(a.Id, a.Name, a.Type, a.LifecycleState, a.Location, a.County,
                stats.GetValueOrDefault(a.Id, UnitStats.Empty), PropertyRules.Effective(a.Purpose, a.Type)))
            .ToList();
    }

    public async Task<PortfolioTotals> GetTotalsAsync(CancellationToken ct = default)
    {
        var count = await db.Assets.CountAsync(ct);
        var stats = await LoadStatsAsync(null, ct);
        return new PortfolioTotals(count, UnitStats.Combine(stats.Values));
    }

    public async Task<AssetDetails?> GetDetailsAsync(int assetId, CancellationToken ct = default)
    {
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return null;

        var nodes = await LoadNodesAsync(assetId, ct);
        var units = await db.Units.AsNoTracking()
            .Where(u => u.AssetId == assetId)
            .Select(u => new
            {
                u.Id, u.UnitNumber, u.StructureNodeId, u.FloorNumber, u.Type, u.Status,
                u.Bedrooms, u.SizeSqm, u.MonthlyRent, u.SalePrice
            })
            .ToListAsync(ct);

        var occupants = await db.Leases.AsNoTracking()
            .Where(l => l.Status == LeaseStatus.Active && l.Unit.AssetId == assetId)
            .Select(l => new { l.UnitId, l.Id, l.TenantId, l.Tenant.FullName })
            .ToDictionaryAsync(l => l.UnitId, ct);

        var paths = StructureTree.Paths(nodes);
        var unitCounts = units.Where(u => u.StructureNodeId != null)
            .GroupBy(u => u.StructureNodeId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var summaries = units
            .Select(u => new UnitSummary(u.Id, u.UnitNumber, u.StructureNodeId,
                u.StructureNodeId is int nid ? paths.GetValueOrDefault(nid) : null,
                u.FloorNumber, u.Type, u.Status, u.Bedrooms, u.SizeSqm, u.MonthlyRent, u.SalePrice,
                occupants.TryGetValue(u.Id, out var o) ? o.Id : null,
                occupants.TryGetValue(u.Id, out var o2) ? o2.TenantId : null,
                occupants.TryGetValue(u.Id, out var o3) ? o3.FullName : null))
            .OrderBy(u => u.NodePath ?? "", NaturalComparer.Instance)
            .ThenBy(u => u.UnitNumber, NaturalComparer.Instance)
            .ToList();

        var stats = UnitStats.From(units
            .GroupBy(u => u.Status)
            .Select(g => (g.Key, g.Count(), g.Sum(u => u.MonthlyRent))));

        return new AssetDetails(asset, StructureTree.Flatten(nodes, unitCounts), summaries, stats);
    }

    public async Task<IReadOnlyList<StructureTreeRow>> GetTreeAsync(int assetId, CancellationToken ct = default)
        => StructureTree.Flatten(await LoadNodesAsync(assetId, ct));

    public async Task<Result<int>> CreateAsync(AssetInput input, CancellationToken ct = default)
    {
        var error = await ValidateAsync(input, null, ct);
        if (error is not null) return Result<int>.Fail(error);

        var asset = new Asset();
        Apply(asset, input);
        db.Assets.Add(asset);
        await SaveAndRefreshAsync(asset, ct);
        return Result<int>.Ok(asset.Id);
    }

    public async Task<Result> UpdateAsync(int assetId, AssetInput input, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Property not found.");

        var error = await ValidateAsync(input, assetId, ct);
        if (error is not null) return Result.Fail(error);

        Apply(asset, input);
        await SaveAndRefreshAsync(asset, ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int assetId, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Property not found.");

        var unitCount = await db.Units.CountAsync(u => u.AssetId == assetId, ct);
        if (unitCount > 0)
            return Result.Fail($"{asset.Name} still has {unitCount} unit(s). Delete or move them first.");

        var nodes = await db.StructureNodes.Where(n => n.AssetId == assetId).ToListAsync(ct);
        db.StructureNodes.RemoveRange(nodes);   // soft delete (DbContext converts)
        db.Assets.Remove(asset);
        await SaveAndRefreshAsync(asset, ct);
        return Result.Ok();
    }

    public async Task<Result<int>> AddNodeAsync(int assetId, StructureNodeInput input, CancellationToken ct = default)
    {
        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct))
            return Result<int>.Fail("Property not found.");

        var nodes = await LoadNodesAsync(assetId, ct);
        var error = ValidateNode(nodes, input, nodeId: null);
        if (error is not null) return Result<int>.Fail(error);

        var node = new StructureNode
        {
            AssetId = assetId, ParentId = input.ParentId, Name = input.Name.Trim(),
            NodeType = input.NodeType, SortOrder = input.SortOrder
        };
        db.StructureNodes.Add(node);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(node.Id);
    }

    public async Task<Result> UpdateNodeAsync(int nodeId, StructureNodeInput input, CancellationToken ct = default)
    {
        var node = await db.StructureNodes.FirstOrDefaultAsync(n => n.Id == nodeId, ct);
        if (node is null) return Result.Fail("Block or floor not found.");

        var nodes = await LoadNodesAsync(node.AssetId, ct);
        var error = ValidateNode(nodes, input, nodeId);
        if (error is not null) return Result.Fail(error);

        node.Name = input.Name.Trim();
        node.NodeType = input.NodeType;
        node.ParentId = input.ParentId;
        node.SortOrder = input.SortOrder;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteNodeAsync(int nodeId, CancellationToken ct = default)
    {
        var node = await db.StructureNodes.FirstOrDefaultAsync(n => n.Id == nodeId, ct);
        if (node is null) return Result.Fail("Block or floor not found.");

        if (await db.StructureNodes.AnyAsync(n => n.ParentId == nodeId, ct))
            return Result.Fail($"{node.Name} contains other blocks or floors. Delete those first.");
        if (await db.Units.AnyAsync(u => u.StructureNodeId == nodeId, ct))
            return Result.Fail($"{node.Name} contains units. Move or delete them first.");

        db.StructureNodes.Remove(node);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------------

    private Task<List<StructureNodeDto>> LoadNodesAsync(int assetId, CancellationToken ct)
        => db.StructureNodes.AsNoTracking()
            .Where(n => n.AssetId == assetId)
            .Select(n => new StructureNodeDto(n.Id, n.ParentId, n.Name, n.NodeType, n.SortOrder))
            .ToListAsync(ct);

    private async Task<Dictionary<int, UnitStats>> LoadStatsAsync(int? assetId, CancellationToken ct)
    {
        var query = db.Units.AsNoTracking();
        if (assetId is int id) query = query.Where(u => u.AssetId == id);

        var rows = await query
            .GroupBy(u => new { u.AssetId, u.Status })
            .Select(g => new { g.Key.AssetId, g.Key.Status, Count = g.Count(), Rent = g.Sum(u => u.MonthlyRent) })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.AssetId)
            .ToDictionary(g => g.Key, g => UnitStats.From(g.Select(r => (r.Status, r.Count, r.Rent))));
    }

    private async Task<string?> ValidateAsync(AssetInput input, int? assetId, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (name.Length == 0) return "Enter the property name.";
        if (await db.Assets.AnyAsync(a => a.Name == name && a.Id != (assetId ?? 0), ct))
            return $"You already have a property called {name}.";
        if (!string.IsNullOrWhiteSpace(input.County) && !KenyaCounties.IsValid(input.County))
            return "Choose a county from the list.";
        if (input.LandSizeAcres < 0 || input.AcquisitionCost < 0)
            return "Land size and acquisition cost can't be negative.";
        return null;
    }

    private static string? ValidateNode(IReadOnlyList<StructureNodeDto> nodes, StructureNodeInput input, int? nodeId)
    {
        var name = input.Name.Trim();
        if (name.Length == 0) return "Enter a name, for example Block A or Floor 1.";

        if (input.ParentId is int parentId)
        {
            if (nodes.All(n => n.Id != parentId)) return "The chosen parent isn't part of this property.";
            if (nodeId is int id && StructureTree.IsSelfOrDescendant(nodes, id, parentId))
                return "A block or floor can't be placed inside itself.";
        }

        if (nodes.Any(n => n.ParentId == input.ParentId && n.Id != nodeId &&
                           string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase)))
            return $"There's already a {name} at that level.";

        return null;
    }

    private static void Apply(Asset asset, AssetInput input)
    {
        asset.Name = input.Name.Trim();
        asset.Type = input.Type;
        asset.Purpose = input.Purpose ?? asset.Purpose ?? PropertyRules.DefaultPurpose(input.Type);
        asset.LifecycleState = input.LifecycleState;
        asset.Location = input.Location?.Trim();
        asset.County = KenyaCounties.All.FirstOrDefault(c => string.Equals(c, input.County, StringComparison.OrdinalIgnoreCase));
        asset.LandSizeAcres = input.LandSizeAcres;
        asset.AcquisitionCost = input.AcquisitionCost;
        asset.AcquisitionDate = input.AcquisitionDate;
        asset.TitleDeedNumber = input.TitleDeedNumber?.Trim();
        asset.Description = input.Description?.Trim();
    }
}
