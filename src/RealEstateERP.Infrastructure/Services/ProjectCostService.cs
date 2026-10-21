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
/// Development costing. Depends on: DbContext, IFileStorage, ISystemConfigService, TimeProvider.
/// Reads sales and leases for revenue but never changes them.
/// Expected (forecast) cost of each budget bucket (category + unit) = max(budget, actual).
/// </summary>
public sealed class ProjectCostService(ApplicationDbContext db, IFileStorage storage, ISystemConfigService config, TimeProvider clock) : IProjectCostService
{
    private DateOnly Today => clock.TodayEat();

    // ------------------------------------------------------------------ overview

    public async Task<IReadOnlyList<ProjectSummaryRow>> GetPortfolioAsync(CancellationToken ct = default)
    {
        var ids = await db.Assets.AsNoTracking().OrderBy(a => a.Name).Select(a => a.Id).ToListAsync(ct);
        var rows = new List<ProjectSummaryRow>(ids.Count);
        foreach (var id in ids)
        {
            var o = await GetOverviewAsync(id, ct);
            if (o is null) continue;
            rows.Add(new ProjectSummaryRow(o.Asset.Id, o.Asset.Name, o.Asset.Type, o.Asset.LifecycleState, o.Units.Count,
                o.BudgetTotal, o.ActualTotal, o.ExpectedTotal, o.Sales.ProjectedRevenue, o.Sales.ProjectedMarginPercent,
                o.Rental.AnnualRentRoll, o.Rental.GrossYield, o.CostLines > 0 || o.Asset.AcquisitionCost is not null || o.Asset.EstimatedLandCost is not null));
        }
        return rows;
    }

    public async Task<ProjectOverview?> GetOverviewAsync(int assetId, CancellationToken ct = default)
    {
        var asset = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return null;

        var units = await db.Units.AsNoTracking().Where(u => u.AssetId == assetId && u.Status != UnitStatus.Inactive)
            .Select(u => new { u.Id, u.UnitNumber, u.Type, u.Status, u.SizeSqm, u.TitleNumber, u.SalePrice, u.MonthlyRent })
            .ToListAsync(ct);
        var costs = await db.ProjectCosts.AsNoTracking().Where(c => c.AssetId == assetId)
            .Select(c => new { c.UnitId, c.Kind, c.Category, c.Amount, c.Date }).ToListAsync(ct);
        var sales = await db.Sales.AsNoTracking().Where(s => s.Unit.AssetId == assetId && s.Status != SaleStatus.Cancelled)
            .Select(s => new { s.UnitId, s.Id, s.Price, s.AmountPaid, BuyerName = s.Buyer.FullName }).ToListAsync(ct);
        var leases = await db.Leases.AsNoTracking().Where(l => l.Unit.AssetId == assetId && l.Status == LeaseStatus.Active)
            .Select(l => new { l.UnitId, l.MonthlyRent }).ToListAsync(ct);

        // ---- budget vs actual by category (land price first) ----
        var categories = new List<CategoryRow>();
        var landBudget = asset.EstimatedLandCost ?? asset.AcquisitionCost ?? 0m;
        var landActual = asset.AcquisitionCost ?? 0m;
        var landExpected = asset.AcquisitionCost ?? asset.EstimatedLandCost ?? 0m;
        if (landBudget != 0 || landActual != 0) categories.Add(new CategoryRow("Land purchase price", CostGroup.Purchase, null, landBudget, landActual, landExpected));

        var buckets = costs.GroupBy(c => (c.Category, c.UnitId))
            .Select(g => new
            {
                g.Key.Category, g.Key.UnitId,
                Budget = g.Where(c => c.Kind == CostKind.Budget).Sum(c => c.Amount),
                Actual = g.Where(c => c.Kind == CostKind.Actual).Sum(c => c.Amount)
            })
            .Select(b => new { b.Category, b.UnitId, b.Budget, b.Actual, Expected = CostingRules.Expected(b.Budget, b.Actual) })
            .ToList();
        categories.AddRange(buckets.GroupBy(b => b.Category).OrderBy(g => g.Key)
            .Select(g => new CategoryRow(DisplayName(g.Key), CostingRules.Group(g.Key), g.Key, g.Sum(b => b.Budget), g.Sum(b => b.Actual), g.Sum(b => b.Expected))));

        var development = categories.Where(c => c.Category is null || CostingRules.IsDevelopmentCost(c.Category.Value)).ToList();

        // ---- spread shared expected cost over units, add direct costs ----
        var devBuckets = buckets.Where(b => CostingRules.IsDevelopmentCost(b.Category)).ToList();
        var sharedExpected = landExpected + devBuckets.Where(b => b.UnitId is null).Sum(b => b.Expected);
        var unitIds = units.Select(u => u.Id).ToHashSet();
        // Direct costs on a unit that's since been made inactive stay in the project total but on no unit row.
        var direct = devBuckets.Where(b => b.UnitId is int id && unitIds.Contains(id)).GroupBy(b => b.UnitId!.Value).ToDictionary(g => g.Key, g => g.Sum(b => b.Expected));

        var saleByUnit = sales.ToDictionary(s => s.UnitId);
        var leaseByUnit = leases.GroupBy(l => l.UnitId).ToDictionary(g => g.Key, g => g.First().MonthlyRent);
        var (shares, used) = CostingRules.Allocate(sharedExpected,
            units.Select(u => new AllocationUnit(u.Id, u.SizeSqm, saleByUnit.TryGetValue(u.Id, out var s) ? s.Price : u.SalePrice)).ToList(),
            asset.CostAllocation);

        var unitRows = units.Select(u =>
        {
            var shared = shares.GetValueOrDefault(u.Id);
            var directCost = direct.GetValueOrDefault(u.Id);
            if (saleByUnit.TryGetValue(u.Id, out var sale))
                return new UnitMargin(u.Id, u.UnitNumber, u.Type, u.Status, u.SizeSqm, u.TitleNumber, shared, directCost, UnitRevenueMode.Sale,
                    sale.Price, true, sale.AmountPaid, sale.Id, sale.BuyerName, 0, false);
            if (leaseByUnit.TryGetValue(u.Id, out var rent))
                return new UnitMargin(u.Id, u.UnitNumber, u.Type, u.Status, u.SizeSqm, u.TitleNumber, shared, directCost, UnitRevenueMode.Let,
                    null, false, 0, null, null, rent, true);
            if (u.SalePrice is decimal price)
                return new UnitMargin(u.Id, u.UnitNumber, u.Type, u.Status, u.SizeSqm, u.TitleNumber, shared, directCost, UnitRevenueMode.Sale,
                    price, false, 0, null, null, 0, false);
            return new UnitMargin(u.Id, u.UnitNumber, u.Type, u.Status, u.SizeSqm, u.TitleNumber, shared, directCost,
                u.MonthlyRent > 0 ? UnitRevenueMode.Let : UnitRevenueMode.None, null, false, 0, null, null, u.MonthlyRent, false);
        })
        .OrderBy(u => u.UnitNumber, NaturalComparer.Instance)
        .ToList();

        // ---- sales position ----
        var forSale = unitRows.Where(u => u.Mode == UnitRevenueMode.Sale).ToList();
        var contracted = forSale.Where(u => u.Contracted).ToList();
        var unsold = forSale.Where(u => !u.Contracted).ToList();
        var saleCost = forSale.Sum(u => u.Cost);
        var salesPosition = new SalesPosition(forSale.Count, contracted.Count, contracted.Sum(u => u.Price ?? 0), contracted.Sum(u => u.Collected),
            unsold.Count, unsold.Sum(u => u.Price ?? 0), saleCost,
            forSale.Count == 0 ? null : CostingRules.UnitsToBreakEven(saleCost, contracted.Sum(u => u.Price ?? 0), unsold.Select(u => u.Price ?? 0)));

        // ---- rental position (last 12 months) ----
        var letUnits = unitRows.Where(u => u.Mode == UnitRevenueMode.Let).ToList();
        var since = Today.AddMonths(-12);
        var collected = 0m;
        if (letUnits.Count > 0)
        {
            var allocations = await db.PaymentAllocations.AsNoTracking()
                .Where(a => !a.IsReversed && a.Payment.Status == PaymentStatus.Active && a.Payment.PaymentDate > since
                            && a.Invoice.Lease != null && a.Invoice.Lease.Unit.AssetId == assetId)
                .Select(a => new { a.InvoiceId, a.Amount, a.Invoice.Total }).ToListAsync(ct);
            var invoiceIds = allocations.Select(a => a.InvoiceId).Distinct().ToList();
            var deposits = await db.InvoiceLines.AsNoTracking()
                .Where(l => invoiceIds.Contains(l.InvoiceId) && l.LineType == InvoiceLineType.Deposit)
                .GroupBy(l => l.InvoiceId).Select(g => new { g.Key, Total = g.Sum(l => l.LineTotal) })
                .ToDictionaryAsync(x => x.Key, x => x.Total, ct);
            collected = allocations.Sum(a => AgencyRules.IncomePart(a.Amount, a.Total, deposits.GetValueOrDefault(a.InvoiceId)));
        }
        var lettingCosts = costs.Where(c => c.Kind == CostKind.Actual && CostingRules.IsRunningCost(c.Category) && c.Date > since).Sum(c => c.Amount);
        var rental = new RentalPosition(letUnits.Count, letUnits.Count(u => u.IsLet), letUnits.Sum(u => u.Cost), letUnits.Sum(u => u.MonthlyRent * 12), collected, lettingCosts);

        // ---- subdivision ----
        var plots = units.Where(u => u.Type == UnitType.Plot).ToList();
        var subdivision = new SubdivisionPosition(asset.LandSizeAcres is decimal acres ? Math.Round(acres * CostingRules.SqmPerAcre, 0) : null,
            plots.Sum(p => p.SizeSqm ?? 0), plots.Count, plots.Count(p => !string.IsNullOrWhiteSpace(p.TitleNumber)));

        return new ProjectOverview(asset, categories,
            development.Sum(c => c.Budget), development.Sum(c => c.Actual), development.Sum(c => c.Expected),
            unitRows, used, salesPosition, rental, subdivision, costs.Count);
    }

    // ------------------------------------------------------------------ ledger

    public async Task<IReadOnlyList<CostLine>> ListCostsAsync(int assetId, CostKind? kind, CostCategory? category, CancellationToken ct = default)
    {
        var query = db.ProjectCosts.AsNoTracking().Where(c => c.AssetId == assetId);
        if (kind is CostKind k) query = query.Where(c => c.Kind == k);
        if (category is CostCategory cat) query = query.Where(c => c.Category == cat);
        return await query.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).Select(Line()).ToListAsync(ct);
    }

    public Task<CostLine?> GetCostAsync(int costId, CancellationToken ct = default)
        => db.ProjectCosts.AsNoTracking().Where(c => c.Id == costId).Select(Line()).FirstOrDefaultAsync(ct);

    public async Task<Result<int>> SaveCostAsync(int? costId, CostInput input, UploadedFile? receipt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Description)) return Result<int>.Fail("Describe the cost, e.g. Murram road, phase 1.");
        if (input.Amount <= 0) return Result<int>.Fail("Enter the amount.");
        if (input.Kind == CostKind.Actual && input.Date is null) return Result<int>.Fail("Enter the date the cost was incurred.");
        if (input.Date is DateOnly d && input.Kind == CostKind.Actual && d > Today) return Result<int>.Fail("An actual cost can't be dated in the future. Record it as a budget instead.");
        if (!await db.Assets.AnyAsync(a => a.Id == input.AssetId, ct)) return Result<int>.Fail("Property not found.");
        if (input.UnitId is int u && !await db.Units.AnyAsync(x => x.Id == u && x.AssetId == input.AssetId, ct)) return Result<int>.Fail("That unit isn't part of this property.");

        ProjectCost cost;
        if (costId is int id)
        {
            var found = await db.ProjectCosts.FirstOrDefaultAsync(c => c.Id == id && c.AssetId == input.AssetId, ct);
            if (found is null) return Result<int>.Fail("Cost not found.");
            cost = found;
        }
        else
        {
            cost = new ProjectCost { AssetId = input.AssetId };
            db.ProjectCosts.Add(cost);
        }

        if (receipt is not null)
        {
            var error = UploadValidator.Validate(receipt.Content, receipt.FileName, receipt.Length, await config.GetIntAsync(ConfigKeys.MaxProofSizeMb, 5, ct));
            if (error is not null) return Result<int>.Fail(error);
            cost.ReceiptStorageKey = await storage.SaveAsync(receipt.Content, "cost-receipts", Path.GetExtension(receipt.FileName), ct);
            cost.ReceiptFileName = Path.GetFileName(receipt.FileName);
        }

        cost.UnitId = input.UnitId;
        cost.Kind = input.Kind;
        cost.Category = input.Category;
        cost.Description = input.Description.Trim();
        cost.Amount = input.Amount;
        cost.Date = input.Date;
        cost.Supplier = input.Supplier?.Trim();
        cost.Reference = input.Reference?.Trim();
        cost.Notes = input.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(cost.Id);
    }

    public async Task<Result> DeleteCostAsync(int costId, CancellationToken ct = default)
    {
        var cost = await db.ProjectCosts.FirstOrDefaultAsync(c => c.Id == costId, ct);
        if (cost is null) return Result.Fail("Cost not found.");
        db.ProjectCosts.Remove(cost);   // soft delete: kept for audit
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<(string Key, string FileName)?> GetReceiptAsync(int costId, CancellationToken ct = default)
    {
        var c = await db.ProjectCosts.AsNoTracking().Where(x => x.Id == costId && x.ReceiptStorageKey != null)
            .Select(x => new { x.ReceiptStorageKey, x.ReceiptFileName }).FirstOrDefaultAsync(ct);
        return c is null ? null : (c.ReceiptStorageKey!, c.ReceiptFileName ?? "receipt");
    }

    // ------------------------------------------------------------------ setup

    public async Task<Result> SaveSetupAsync(int assetId, ProjectSetupInput input, CancellationToken ct = default)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.Id == assetId, ct);
        if (asset is null) return Result.Fail("Property not found.");
        if (input.AcquisitionCost is < 0 || input.EstimatedLandCost is < 0) return Result.Fail("Amounts can't be negative.");
        if (input.LandSizeAcres is <= 0) return Result.Fail("Enter the land size in acres, or leave it blank.");
        if (input.AcquisitionDate is DateOnly bought && bought > Today) return Result.Fail("The purchase date can't be in the future.");

        asset.AcquisitionCost = input.AcquisitionCost;
        asset.AcquisitionDate = input.AcquisitionDate;
        asset.SellerName = input.SellerName?.Trim();
        asset.EstimatedLandCost = input.EstimatedLandCost;
        asset.TitleDeedNumber = input.TitleDeedNumber?.Trim().ToUpperInvariant();
        asset.LandSizeAcres = input.LandSizeAcres;
        asset.CostAllocation = input.CostAllocation;
        asset.SubdivisionReference = input.SubdivisionReference?.Trim();
        asset.SubdivisionApprovedOn = input.SubdivisionApprovedOn;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetUnitTitleAsync(int unitId, string? titleNumber, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return Result.Fail("Unit not found.");
        var title = string.IsNullOrWhiteSpace(titleNumber) ? null : titleNumber.Trim().ToUpperInvariant();
        if (title is not null && await db.Units.AnyAsync(u => u.Id != unitId && u.TitleNumber == title, ct))
            return Result.Fail($"Title {title} is already on another unit.");
        unit.TitleNumber = title;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------------

    private static System.Linq.Expressions.Expression<Func<ProjectCost, CostLine>> Line()
        => c => new CostLine(c.Id, c.UnitId, c.Unit != null ? c.Unit.UnitNumber : null, c.Kind, c.Category, c.Description, c.Amount,
            c.Date, c.Supplier, c.Reference, c.ReceiptStorageKey != null, c.Notes);

    private static string DisplayName(CostCategory c) => c switch
    {
        CostCategory.PurchaseCosts => "Purchase costs",
        CostCategory.ProfessionalFees => "Professional fees",
        CostCategory.Finance => "Finance costs",
        CostCategory.Holding => "Holding costs",
        _ when CostingRules.IsRunningCost(c) => RealEstateERP.Core.Common.EnumNames.Display(c),
        _ => c.ToString()
    };
}
