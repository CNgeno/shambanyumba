using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.Models;

/// <summary>One development project at a glance: used on the developer dashboard and on a land property's page.</summary>
public sealed record ProjectCard(
    int AssetId, string Name, AssetType Type, decimal? Acres, string? Title,
    int Plots, int Available, int Reserved, int Sold,
    decimal ExpectedCost, decimal Spent, decimal ProjectedRevenue, decimal? MarginPercent,
    decimal Collected, decimal Overdue, int? UnitsToBreakEven, bool HasCosts)
{
    public int SoldPercent => Plots == 0 ? 0 : (int)Math.Round(Sold * 100m / Plots);

    public static ProjectCard From(ProjectOverview o, decimal overdue)
    {
        var plots = o.Units.Where(u => u.Mode == UnitRevenueMode.Sale || u.Type == UnitType.Plot).ToList();
        return new ProjectCard(o.Asset.Id, o.Asset.Name, o.Asset.Type, o.Asset.LandSizeAcres, o.Asset.TitleDeedNumber,
            plots.Count, plots.Count(u => u.Status == UnitStatus.Available), plots.Count(u => u.Status == UnitStatus.Reserved),
            plots.Count(u => u.Status == UnitStatus.Sold),
            o.ExpectedTotal, o.ActualTotal, o.Sales.ProjectedRevenue, o.Sales.ProjectedMarginPercent,
            o.Sales.Collected, overdue, o.Sales.UnitsToBreakEven, o.Categories.Count > 0);
    }
}

public sealed record LandProjectModel(ProjectCard Card, ProjectOverview Overview, bool CanEdit);

/// <summary>One property to let at a glance: occupancy, this month's rent money, overdue, and the next setup step.</summary>
public sealed record RentalCard(
    int AssetId, string Name, AssetType Type, string? Location,
    int Units, int Occupied, int Vacant, decimal RentRoll, decimal OccupiedRent,
    decimal Invoiced, decimal Collected, decimal Overdue, int OverdueInvoices,
    string? NextStep, decimal? NetThisMonth = null)
{
    public int OccupancyPercent => Units == 0 ? 0 : (int)Math.Round(Occupied * 100m / Units);
    public int CollectedPercent => Invoiced <= 0 ? 0 : (int)Math.Min(100, Math.Round(Collected * 100m / Invoiced));
}

/// <summary>A dashboard card: a property for sale (Project) or to let (Rental).</summary>
public sealed record PropertyCardItem(string Name, ProjectCard? Project, RentalCard? Rental);
