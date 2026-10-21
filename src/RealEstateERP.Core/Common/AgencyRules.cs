using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure agency rules (FR-10). No database; fully unit-tested.</summary>
public static class AgencyRules
{
    public static string? ValidateContract(DateOnly start, DateOnly? end, CommissionType type, decimal percent, decimal fixedFee, int remittanceDay)
    {
        if (end is DateOnly e && e <= start) return "The end date must be after the start date.";
        if (remittanceDay is < 1 or > 28) return "The owner payment day must be between 1 and 28.";
        return type switch
        {
            CommissionType.PercentOfCollections when percent is <= 0 or > 50 => "Enter a commission between 0 and 50%.",
            CommissionType.FixedMonthly when fixedFee <= 0 => "Enter the fixed monthly fee.",
            _ => null
        };
    }

    /// <summary>
    /// The part of a payment allocation that counts as rent income: deposits are held for the tenant, so the
    /// deposit share of the invoice is left out (pro rata).
    /// </summary>
    public static decimal IncomePart(decimal allocated, decimal invoiceTotal, decimal invoiceDepositTotal)
    {
        if (invoiceTotal <= 0 || invoiceDepositTotal <= 0) return allocated;
        var depositShare = Math.Min(1m, invoiceDepositTotal / invoiceTotal);
        return Math.Round(allocated * (1 - depositShare), 2, MidpointRounding.AwayFromZero);
    }

    public static decimal Commission(CommissionType type, decimal percent, decimal fixedFee, decimal collections)
        => type == CommissionType.FixedMonthly
            ? fixedFee
            : Math.Round(collections * percent / 100m, 2, MidpointRounding.AwayFromZero);

    public static decimal NetPayable(decimal collections, decimal commission, decimal adjustments) => collections - commission + adjustments;

    public static decimal AgentShare(decimal commission, decimal sharePercent)
        => Math.Round(commission * Math.Clamp(sharePercent, 0, 100) / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Was the property covered by the contract at any point in the period?</summary>
    public static bool CoveredDuring(DateOnly addedOn, DateOnly? removedOn, DateOnly periodStart, DateOnly periodEnd)
        => addedOn <= periodEnd && (removedOn is null || removedOn >= periodStart);
}
