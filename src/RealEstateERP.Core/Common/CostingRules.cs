using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

public sealed record AllocationUnit(int UnitId, decimal? SizeSqm, decimal? Value);

/// <summary>Pure development-costing rules. No database; fully unit-tested.</summary>
public static class CostingRules
{
    public const decimal SqmPerAcre = 4046.86m;

    public static CostGroup Group(CostCategory c) => c switch
    {
        CostCategory.PurchaseCosts => CostGroup.Purchase,
        CostCategory.Construction or CostCategory.Infrastructure or CostCategory.ProfessionalFees or CostCategory.Approvals => CostGroup.Development,
        CostCategory.Subdivision => CostGroup.Subdivision,
        >= CostCategory.Letting => CostGroup.Letting,
        _ => CostGroup.Other
    };

    /// <summary>Letting costs are running costs that reduce rental income; they're not part of the development cost.</summary>
    public static bool IsDevelopmentCost(CostCategory c) => c < CostCategory.Letting;

    /// <summary>The owner's running costs of a property to let (repairs, cleaning, advertising...): reduce rental income.</summary>
    public static bool IsRunningCost(CostCategory c) => c >= CostCategory.Letting;

    /// <summary>Forecast final cost of a budget line: spending beyond the budget counts; unspent budget is still expected.</summary>
    public static decimal Expected(decimal budget, decimal actual) => Math.Max(budget, actual);

    /// <summary>
    /// Spreads a shared total over units. Falls back to equal shares when the chosen basis is missing for any unit
    /// (no size, or no value). Rounded to the cent; the remainder goes to the unit with the largest share.
    /// </summary>
    public static (IReadOnlyDictionary<int, decimal> Shares, CostAllocationMethod Used) Allocate(
        decimal total, IReadOnlyList<AllocationUnit> units, CostAllocationMethod method)
    {
        if (units.Count == 0) return (new Dictionary<int, decimal>(), method);

        var used = method switch
        {
            CostAllocationMethod.BySize when units.All(u => u.SizeSqm is > 0) => CostAllocationMethod.BySize,
            CostAllocationMethod.ByValue when units.All(u => u.Value is > 0) => CostAllocationMethod.ByValue,
            _ => CostAllocationMethod.Equal
        };
        var weights = units.ToDictionary(u => u.UnitId, u => used switch
        {
            CostAllocationMethod.BySize => u.SizeSqm!.Value,
            CostAllocationMethod.ByValue => u.Value!.Value,
            _ => 1m
        });
        var weightTotal = weights.Values.Sum();
        var shares = weights.ToDictionary(w => w.Key, w => Math.Round(total * w.Value / weightTotal, 2, MidpointRounding.AwayFromZero));
        var remainder = total - shares.Values.Sum();
        if (remainder != 0)
        {
            var biggest = weights.OrderByDescending(w => w.Value).ThenBy(w => w.Key).First().Key;
            shares[biggest] += remainder;
        }
        return (shares, used);
    }

    public static decimal? MarginPercent(decimal revenue, decimal cost) => revenue <= 0 ? null : Math.Round((revenue - cost) * 100 / revenue, 1);

    public static decimal? YieldPercent(decimal annualIncome, decimal cost) => cost <= 0 ? null : Math.Round(annualIncome * 100 / cost, 1);

    public static int? PaybackMonths(decimal cost, decimal monthlyIncome) => monthlyIncome <= 0 ? null : (int)Math.Ceiling(cost / monthlyIncome);

    /// <summary>
    /// How many more unsold units must sell (cheapest first, so it's never optimistic) for agreed sales to cover the
    /// expected cost. 0 = already covered; null = even selling everything at asking price won't cover it.
    /// </summary>
    public static int? UnitsToBreakEven(decimal expectedCost, decimal contractedRevenue, IEnumerable<decimal> unsoldPrices)
    {
        var gap = expectedCost - contractedRevenue;
        if (gap <= 0) return 0;
        var count = 0;
        foreach (var price in unsoldPrices.OrderBy(p => p))
        {
            gap -= price;
            count++;
            if (gap <= 0) return count;
        }
        return null;
    }
}
