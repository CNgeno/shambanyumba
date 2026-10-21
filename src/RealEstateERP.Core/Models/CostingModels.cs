using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record CostInput(int AssetId, int? UnitId, CostKind Kind, CostCategory Category, string Description, decimal Amount,
    DateOnly? Date, string? Supplier, string? Reference, string? Notes);

public sealed record ProjectSetupInput(
    decimal? AcquisitionCost, DateOnly? AcquisitionDate, string? SellerName, decimal? EstimatedLandCost,
    string? TitleDeedNumber, decimal? LandSizeAcres, CostAllocationMethod CostAllocation,
    string? SubdivisionReference, DateOnly? SubdivisionApprovedOn);

public sealed record CostLine(
    int Id, int? UnitId, string? UnitNumber, CostKind Kind, CostCategory Category, string Description, decimal Amount,
    DateOnly? Date, string? Supplier, string? Reference, bool HasReceipt, string? Notes);

/// <summary>One row of the budget-vs-actual table. Land price appears as its own row.</summary>
public sealed record CategoryRow(string Label, CostGroup Group, CostCategory? Category, decimal Budget, decimal Actual, decimal Expected)
{
    public decimal Variance => Actual - Budget;   // positive = over budget so far
}

public sealed record UnitMargin(
    int UnitId, string UnitNumber, UnitType Type, UnitStatus Status, decimal? SizeSqm, string? TitleNumber,
    decimal SharedCost, decimal DirectCost, UnitRevenueMode Mode,
    decimal? Price, bool Contracted, decimal Collected, int? SaleId, string? BuyerName,
    decimal MonthlyRent, bool IsLet)
{
    public decimal Cost => SharedCost + DirectCost;
    public decimal? Margin => Mode == UnitRevenueMode.Sale && Price is decimal p ? p - Cost : null;
    public decimal? MarginPercent => Mode == UnitRevenueMode.Sale && Price is decimal p ? Common.CostingRules.MarginPercent(p, Cost) : null;
    public decimal? GrossYield => Mode == UnitRevenueMode.Let ? Common.CostingRules.YieldPercent(MonthlyRent * 12, Cost) : null;
    public int? PaybackMonths => Mode == UnitRevenueMode.Let ? Common.CostingRules.PaybackMonths(Cost, MonthlyRent) : null;
}

public sealed record SalesPosition(
    int UnitsForSale, int Contracted, decimal ContractedRevenue, decimal Collected, int Unsold, decimal UnsoldValue,
    decimal Cost, int? UnitsToBreakEven)
{
    public decimal ProjectedRevenue => ContractedRevenue + UnsoldValue;
    public decimal ProjectedMargin => ProjectedRevenue - Cost;
    public decimal? ProjectedMarginPercent => Common.CostingRules.MarginPercent(ProjectedRevenue, Cost);
}

public sealed record RentalPosition(int LetUnits, int Occupied, decimal Cost, decimal AnnualRentRoll, decimal Collected12m, decimal LettingCosts12m)
{
    public decimal NetIncome12m => Collected12m - LettingCosts12m;
    public decimal? GrossYield => Common.CostingRules.YieldPercent(AnnualRentRoll, Cost);
    public decimal? NetYield => Common.CostingRules.YieldPercent(NetIncome12m, Cost);
}

public sealed record SubdivisionPosition(decimal? TotalAreaSqm, decimal PlottedSqm, int Plots, int PlotsWithTitles)
{
    public decimal? SurrenderedSqm => TotalAreaSqm is decimal t ? Math.Max(0, t - PlottedSqm) : null;
    public decimal? SurrenderedPercent => TotalAreaSqm is decimal t && t > 0 ? Math.Round(Math.Max(0, t - PlottedSqm) * 100 / t, 1) : null;
}

public sealed record ProjectOverview(
    Asset Asset,
    IReadOnlyList<CategoryRow> Categories,
    decimal BudgetTotal, decimal ActualTotal, decimal ExpectedTotal,
    IReadOnlyList<UnitMargin> Units,
    CostAllocationMethod AllocationUsed,
    SalesPosition Sales,
    RentalPosition Rental,
    SubdivisionPosition Subdivision,
    int CostLines);

public sealed record ProjectSummaryRow(
    int AssetId, string Name, AssetType Type, AssetLifecycle Lifecycle, int Units,
    decimal BudgetTotal, decimal ActualTotal, decimal ExpectedTotal,
    decimal ProjectedSalesRevenue, decimal? ProjectedMarginPercent, decimal AnnualRentRoll, decimal? GrossYield, bool HasCosts);
