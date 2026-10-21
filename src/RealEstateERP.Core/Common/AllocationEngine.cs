using System.Text.RegularExpressions;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Common;

public sealed record OpenInvoiceItem(int InvoiceId, DateOnly DueDate, DateOnly InvoiceDate, decimal Balance);

/// <summary>Pure allocation rules (FR-08-B). No database; fully unit-tested.</summary>
public static partial class AllocationEngine
{
    /// <summary>Oldest due date first, then invoice date, then id. Never allocates more than a balance or the amount.</summary>
    public static (IReadOnlyList<(int InvoiceId, decimal Amount)> Allocations, decimal Unallocated) OldestFirst(decimal amount, IEnumerable<OpenInvoiceItem> items)
    {
        var result = new List<(int, decimal)>();
        var left = amount;
        foreach (var item in items.Where(i => i.Balance > 0).OrderBy(i => i.DueDate).ThenBy(i => i.InvoiceDate).ThenBy(i => i.InvoiceId))
        {
            if (left <= 0) break;
            var take = Math.Min(left, item.Balance);
            result.Add((item.InvoiceId, take));
            left -= take;
        }
        return (result, left);
    }

    public static string? ValidateManual(decimal amount, IReadOnlyDictionary<int, decimal> requested, IReadOnlyDictionary<int, decimal> balances)
    {
        foreach (var (invoiceId, value) in requested)
        {
            if (value < 0) return "Allocations can't be negative.";
            if (!balances.TryGetValue(invoiceId, out var balance)) return "One of the invoices isn't open for this tenant.";
            if (value > balance) return "An allocation is more than the invoice's balance.";
        }
        return requested.Values.Sum() > amount ? "The allocations add up to more than the payment." : null;
    }

    /// <summary>Returns the cleaned reference or an error. M-PESA codes are 10 letters/digits (e.g. SJK4H7QX2P).</summary>
    public static (string? Reference, string? Error) NormalizeReference(PaymentMethod method, string? reference)
    {
        var value = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim().ToUpperInvariant();
        return method switch
        {
            PaymentMethod.Mpesa when value is null || !MpesaCode().IsMatch(value) => (null, "Enter the 10-character M-PESA code, e.g. SJK4H7QX2P."),
            PaymentMethod.BankTransfer or PaymentMethod.Cheque when value is null || value.Length < 3 => (null, method == PaymentMethod.Cheque ? "Enter the cheque number." : "Enter the bank reference."),
            _ => (value, null)
        };
    }

    [GeneratedRegex("^[A-Z0-9]{10}$")]
    private static partial Regex MpesaCode();
}
