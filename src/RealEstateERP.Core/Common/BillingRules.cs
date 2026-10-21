using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

/// <summary>Pure billing rules. No database; fully unit-tested.</summary>
public static class BillingRules
{
    public static (DateOnly Start, DateOnly End) MonthBounds(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }

    /// <summary>
    /// Rent for the period. Only the FIRST month is prorated (lease starting mid-month).
    /// The last month is billed in full: tenants often stay on, and final bills are made explicitly.
    /// </summary>
    public static (decimal Amount, int DaysBilled, int DaysInPeriod) RentForPeriod(decimal monthlyRent, DateOnly leaseStart, DateOnly periodStart, DateOnly periodEnd)
    {
        var daysInPeriod = periodEnd.DayNumber - periodStart.DayNumber + 1;
        if (leaseStart <= periodStart) return (monthlyRent, daysInPeriod, daysInPeriod);
        if (leaseStart > periodEnd) return (0m, 0, daysInPeriod);

        var days = periodEnd.DayNumber - leaseStart.DayNumber + 1;
        return (Math.Round(monthlyRent * days / daysInPeriod, 2, MidpointRounding.AwayFromZero), days, daysInPeriod);
    }

    /// <summary>The rent due day in the billed month (clamped to month length), never before the lease starts.</summary>
    public static DateOnly DueDate(DateOnly periodStart, int rentDueDay, DateOnly leaseStart)
    {
        var day = Math.Clamp(rentDueDay, 1, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
        var due = new DateOnly(periodStart.Year, periodStart.Month, day);
        return due < leaseStart ? leaseStart : due;
    }

    /// <summary>Deposit goes on the first invoice of a brand-new lease only (not renewals, not onboarded tenants).</summary>
    public static bool ShouldInvoiceDeposit(decimal deposit, bool isRenewal, DateOnly leaseStart, DateOnly periodStart, bool alreadyInvoiced)
        => deposit > 0 && !isRenewal && !alreadyInvoiced && leaseStart >= periodStart;

    public static (decimal Amount, decimal Tax, decimal Total) LineMath(decimal quantity, decimal unitPrice, decimal taxPercent)
    {
        var amount = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        var tax = Math.Round(amount * taxPercent / 100m, 2, MidpointRounding.AwayFromZero);
        return (amount, tax, amount + tax);
    }

    public static InvoiceStatus EffectiveStatus(InvoiceStatus stored, decimal total, decimal paid, DateOnly dueDate, DateOnly today)
    {
        if (stored is not InvoiceStatus.Pending) return stored;   // Draft, Cancelled, WrittenOff
        if (paid >= total) return InvoiceStatus.Paid;
        if (dueDate < today) return InvoiceStatus.Overdue;
        return paid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Pending;
    }

    public static string? ReadingError(decimal? previous, decimal current)
    {
        if (current < 0) return "A meter reading can't be negative.";
        if (previous is decimal p && current < p) return $"The reading ({current:0.###}) is lower than the previous one ({p:0.###}). Check it, or record a meter replacement as a new opening reading.";
        return null;
    }

    /// <summary>Is a recurring charge billed in this month? Quarterly/annual charges fall due in the anchor month (and every 3 months after, for quarterly).</summary>
    public static bool ChargeDue(ChargeFrequency frequency, int anchorMonth, int periodMonth)
    {
        var anchor = Math.Clamp(anchorMonth, 1, 12);
        return frequency switch
        {
            ChargeFrequency.Quarterly => ((periodMonth - anchor) % 3 + 3) % 3 == 0,
            ChargeFrequency.Annual => periodMonth == anchor,
            _ => true
        };
    }

    public static string FrequencyLabel(ChargeFrequency frequency) => frequency switch
    {
        ChargeFrequency.Quarterly => "quarterly",
        ChargeFrequency.Annual => "annual",
        _ => "monthly"
    };
}
