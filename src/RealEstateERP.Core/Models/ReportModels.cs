using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record MonthCollections(DateOnly Month, decimal Invoiced, decimal Collected)
{
    public decimal? Rate => Invoiced <= 0 ? null : Math.Round(Collected * 100 / Invoiced, 1);
}

public sealed record RevenueRow(string Label, int Units, decimal Invoiced, decimal Collected, decimal Expenses = 0)
{
    /// <summary>Collected rent less the owner's running costs (repairs, cleaning, advertising...).</summary>
    public decimal Net => Collected - Expenses;
}

public sealed record OccupancyRow(int AssetId, string AssetName, int Units, int Occupied, int Vacant, int LeasesEnded12m)
{
    public decimal? OccupancyPercent => Units == 0 ? null : Math.Round(Occupied * 100m / Units, 1);
    public decimal? TurnoverPercent => Units == 0 ? null : Math.Round(LeasesEnded12m * 100m / Units, 1);
}

public sealed record VacantUnitRow(int UnitId, string UnitNumber, string AssetName, decimal MonthlyRent, DateOnly VacantSince, int DaysVacant)
{
    public decimal LostRentEstimate => Math.Round(MonthlyRent * DaysVacant / 30m, 0);
}

public sealed record BusinessReports(
    IReadOnlyList<MonthCollections> Collections,
    IReadOnlyList<RevenueRow> ByProperty,
    IReadOnlyList<RevenueRow> ByUnitType,
    IReadOnlyList<OccupancyRow> Occupancy,
    IReadOnlyList<VacantUnitRow> Vacant,
    DateOnly From,
    DateOnly To);

public sealed record PlatformMonth(DateOnly Month, int NewBusinesses, decimal SubscriptionRevenue, decimal CreditSales, int CreditsUsed);

public sealed record PlatformReports(
    decimal Mrr, int Trial, int Active, int Grace, int Suspended, int ChurnedLast30Days,
    IReadOnlyList<PlatformMonth> Months, IReadOnlyList<(string Plan, int Businesses, decimal Mrr)> ByPlan);

public sealed record NotificationRow(int Id, NotificationType Type, string Title, string? Body, string? Url, DateTime CreatedAtUtc, bool IsUnread);

/// <summary>One property's rent money: invoiced and collected this month, and overdue now.</summary>
public sealed record PropertyMoney(int AssetId, decimal InvoicedThisMonth, decimal CollectedThisMonth, decimal Overdue, int OverdueInvoices,
    decimal ExpensesThisMonth = 0, decimal Collected12Months = 0, decimal Expenses12Months = 0)
{
    public decimal NetThisMonth => CollectedThisMonth - ExpensesThisMonth;
    public decimal Net12Months => Collected12Months - Expenses12Months;
}
