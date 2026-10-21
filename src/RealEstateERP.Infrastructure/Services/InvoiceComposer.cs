using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Shared invoice arithmetic (a helper, not a service, so it creates no dependency between services).</summary>
public static class InvoiceComposer
{
    public static InvoiceLine Line(InvoiceLineType type, string description, decimal quantity, decimal unitPrice, decimal taxPercent,
        int? chargeDefinitionId = null, int? meterReadingId = null)
        => new()
        {
            LineType = type, Description = description, Quantity = quantity, UnitPrice = unitPrice,
            TaxPercent = taxPercent, ChargeDefinitionId = chargeDefinitionId, MeterReadingId = meterReadingId
        };

    /// <summary>Recomputes every line and the invoice totals. Call before every save.</summary>
    public static void Recalculate(Invoice invoice)
    {
        var order = 0;
        foreach (var line in invoice.Lines)
        {
            var (amount, tax, total) = BillingRules.LineMath(line.Quantity, line.UnitPrice, line.TaxPercent);
            line.Amount = amount;
            line.TaxAmount = tax;
            line.LineTotal = total;
            line.SortOrder = order++;
        }
        invoice.SubTotal = invoice.Lines.Sum(l => l.Amount);
        invoice.TaxTotal = invoice.Lines.Sum(l => l.TaxAmount);
        invoice.Total = invoice.SubTotal + invoice.TaxTotal;
    }

    public static string? ValidateLines(IReadOnlyList<InvoiceLineInput> lines)
    {
        if (lines.Count == 0) return "Add at least one line.";
        foreach (var l in lines)
        {
            if (string.IsNullOrWhiteSpace(l.Description)) return "Every line needs a description.";
            if (l.Quantity <= 0) return "Quantities must be more than zero.";
            if (l.TaxPercent is < 0 or > 100) return "Tax must be between 0 and 100%.";
            if (l.UnitPrice < 0 && l.LineType != InvoiceLineType.Credit) return "Only credit lines can be negative.";
        }
        return null;
    }
}
