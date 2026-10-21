using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

public sealed record ScheduleLine(int Number, string Description, DateOnly DueDate, decimal Amount);

/// <summary>Pure sales rules (FR-11). No database; fully unit-tested.</summary>
public static class SalesRules
{
    public const int MaxInstalments = 120;

    public static string? ValidatePlan(decimal price, decimal deposit, PaymentPlan plan, int count, DateOnly saleDate, DateOnly? firstInstalment)
    {
        if (price <= 0) return "Enter the sale price.";
        if (plan == PaymentPlan.FullPayment) return null;
        if (deposit < 0 || deposit >= price) return "The deposit must be less than the price (use Full payment if it's paid at once).";
        if (count is < 1 or > MaxInstalments) return $"Choose between 1 and {MaxInstalments} instalments.";
        if (firstInstalment is not DateOnly first) return "Choose when the first instalment is due.";
        if (first <= saleDate) return "The first instalment must fall after the sale date.";
        return null;
    }

    /// <summary>
    /// Full payment: one line for the whole price due on the sale date.
    /// Instalments: the deposit (line 0) on the sale date, then the balance in equal parts; any rounding cents go on the last.
    /// </summary>
    public static IReadOnlyList<ScheduleLine> BuildSchedule(decimal price, decimal deposit, PaymentPlan plan, int count,
        InstalmentFrequency frequency, DateOnly saleDate, DateOnly? firstInstalment)
    {
        if (plan == PaymentPlan.FullPayment)
            return [new ScheduleLine(0, "Full payment", saleDate, price)];

        var lines = new List<ScheduleLine>();
        if (deposit > 0) lines.Add(new ScheduleLine(0, "Deposit", saleDate, deposit));

        var balance = price - deposit;
        var each = Math.Floor(balance / count * 100) / 100;   // round down to the cent; the last line takes the rest
        var step = frequency == InstalmentFrequency.Quarterly ? 3 : 1;
        for (var i = 1; i <= count; i++)
        {
            var amount = i == count ? balance - each * (count - 1) : each;
            lines.Add(new ScheduleLine(i, $"Instalment {i} of {count}", firstInstalment!.Value.AddMonths(step * (i - 1)), amount));
        }
        return lines;
    }

    public static InstalmentState State(decimal amount, decimal paid, DateOnly due, DateOnly today)
    {
        if (paid >= amount) return InstalmentState.Paid;
        if (due < today) return InstalmentState.Overdue;
        return paid > 0 ? InstalmentState.PartlyPaid : InstalmentState.Due;
    }

    /// <summary>Milestones go in order; handover only once the price is fully paid.</summary>
    public static string? MilestoneError(MilestoneType type, IReadOnlyCollection<MilestoneType> alreadyDone, decimal balance)
    {
        if (alreadyDone.Contains(type)) return "This step is already complete.";
        var previous = Enum.GetValues<MilestoneType>().Where(t => t < type).Except(alreadyDone).ToList();
        if (previous.Count > 0) return $"Complete {Label(previous[0])} first.";
        if (type == MilestoneType.Handover && balance > 0) return "The buyer must pay in full before handover.";
        return null;
    }

    public static decimal Commission(decimal collected, decimal percent)
        => Math.Round(collected * percent / 100m, 2, MidpointRounding.AwayFromZero);

    public static string Label(MilestoneType t) => t switch
    {
        MilestoneType.Agreement => "the sale agreement",
        MilestoneType.StampDuty => "stamp duty",
        MilestoneType.TitleTransfer => "the title transfer",
        _ => "handover"
    };
}
