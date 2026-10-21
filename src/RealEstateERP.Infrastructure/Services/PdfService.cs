using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Invoice, receipt and statement PDFs. Pure rendering from already-loaded data; no database.</summary>
public sealed class PdfService : IPdfService
{
    private const string Ink = "#0F1D2E";
    private const string Muted = "#66746D";
    private const string Line = "#DFE5EE";
    private const string Brand = "#1554A8";

    static PdfService() => QuestPDF.Settings.License = LicenseType.Community;

    private static string N(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
    private static string D(DateOnly d) => d.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ invoice

    public byte[] Invoice(InvoiceDetails details, BusinessProfile business)
    {
        var inv = details.Invoice;
        var meta = new List<(string, string)>
        {
            ("Invoice no.", inv.InvoiceNumber), ("Date", D(inv.InvoiceDate)), ("Due", D(inv.DueDate))
        };
        if (inv.PeriodStart is DateOnly p) meta.Add(("Period", p.ToString("MMMM yyyy", CultureInfo.InvariantCulture)));

        return Render(business, "INVOICE", meta, col =>
        {
            BilledTo(col, "Billed to", inv.Tenant.FullName,
                inv.Lease is null ? null : $"Unit {inv.Lease.Unit.UnitNumber}, {inv.Lease.Unit.Asset.Name}",
                KenyanPhone.Display(inv.Tenant.Phone));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(1); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.6f); });
                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Description");
                    HeaderCell(h.Cell(), "Qty", true);
                    HeaderCell(h.Cell(), "Price", true);
                    HeaderCell(h.Cell(), "Tax", true);
                    HeaderCell(h.Cell(), "Amount", true);
                });
                foreach (var l in details.Lines)
                {
                    BodyCell(table.Cell(), l.Description);
                    BodyCell(table.Cell(), l.Quantity.ToString("0.###", CultureInfo.InvariantCulture), true);
                    BodyCell(table.Cell(), N(l.UnitPrice), true);
                    BodyCell(table.Cell(), l.TaxAmount == 0 ? "-" : N(l.TaxAmount), true);
                    BodyCell(table.Cell(), N(l.LineTotal), true);
                }
            });

            var totals = new List<(string, string, bool)> { ("Subtotal", N(inv.SubTotal), false) };
            if (inv.TaxTotal != 0) totals.Add(("Tax", N(inv.TaxTotal), false));
            totals.Add(("Total (KES)", N(inv.Total), true));
            if (inv.AmountPaid > 0)
            {
                totals.Add(("Paid", N(inv.AmountPaid), false));
                totals.Add(("Balance due (KES)", N(inv.Balance), true));
            }
            Totals(col, totals);

            if (details.Status is InvoiceStatus.Cancelled or InvoiceStatus.WrittenOff)
                col.Item().Text($"{details.Status.ToString().ToUpperInvariant()}: {inv.StatusReason}").FontColor(Colors.Red.Darken2).Bold();
            if (!string.IsNullOrWhiteSpace(inv.Notes)) col.Item().Text(inv.Notes).FontColor(Muted);
        });
    }

    // ------------------------------------------------------------------ receipt

    public byte[] Receipt(PaymentDetails details, BusinessProfile business)
    {
        var p = details.Payment;
        var meta = new List<(string, string)> { ("Receipt no.", p.ReceiptNumber), ("Date", D(p.PaymentDate)), ("Method", Method(p.Method)) };
        if (p.Reference is not null) meta.Add(("Reference", p.Reference));

        return Render(business, "OFFICIAL RECEIPT", meta, col =>
        {
            if (p.Status == PaymentStatus.Reversed)
                col.Item().Background(Colors.Red.Lighten4).Padding(8)
                    .Text($"REVERSED: {p.ReversalReason}").FontColor(Colors.Red.Darken3).Bold();

            col.Item().Column(c =>
            {
                c.Item().Text($"Received from {p.PayerName ?? p.Tenant.FullName}").FontColor(Muted);
                c.Item().Text($"KES {N(p.Amount)}").FontSize(22).Bold().FontColor(Ink);
                if (p.Lease is not null) c.Item().Text($"For unit {p.Lease.Unit.UnitNumber}, {p.Lease.Unit.Asset.Name}");
            });

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); });
                table.Header(h => { HeaderCell(h.Cell(), "Applied to"); HeaderCell(h.Cell(), "Due"); HeaderCell(h.Cell(), "Amount", true); });
                foreach (var a in details.Allocations)
                {
                    BodyCell(table.Cell(), a.InvoiceNumber + (a.IsReversed ? " (reversed)" : ""));
                    BodyCell(table.Cell(), D(a.DueDate));
                    BodyCell(table.Cell(), N(a.Amount), true);
                }
                foreach (var r in details.Refunds)
                {
                    BodyCell(table.Cell(), $"Refunded ({r.RefundNumber})");
                    BodyCell(table.Cell(), D(r.RefundDate));
                    BodyCell(table.Cell(), N(r.Amount), true);
                }
                if (details.Unallocated > 0)
                {
                    BodyCell(table.Cell(), "Held as credit");
                    BodyCell(table.Cell(), "");
                    BodyCell(table.Cell(), N(details.Unallocated), true);
                }
            });

            var net = details.TenantBalance.Net;
            Totals(col, [("Account balance", net > 0 ? $"{N(net)} owing" : net < 0 ? $"{N(-net)} in credit" : "Nothing owing", true)]);
            col.Item().Text("Thank you.").FontColor(Muted);
        });
    }

    // ------------------------------------------------------------------ statement

    public byte[] Statement(TenantStatement s, BusinessProfile business)
    {
        string Bal(decimal v) => v < 0 ? $"{N(-v)} CR" : N(v);
        var meta = new List<(string, string)>
        {
            ("Tenant", s.Tenant.FullName),
            ("Period", $"{(s.From is DateOnly f ? D(f) : "Start")} to {D(s.To)}"),
            ("Balance", Bal(s.ClosingBalance))
        };

        return Render(business, "TENANT STATEMENT", meta, col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(1.6f); c.RelativeColumn(1.6f); c.RelativeColumn(4); c.RelativeColumn(1.5f); c.RelativeColumn(1.5f); c.RelativeColumn(1.7f); });
                table.Header(h =>
                {
                    HeaderCell(h.Cell(), "Date"); HeaderCell(h.Cell(), "Reference"); HeaderCell(h.Cell(), "Details");
                    HeaderCell(h.Cell(), "Charged", true); HeaderCell(h.Cell(), "Paid", true); HeaderCell(h.Cell(), "Balance", true);
                });
                if (s.From is not null)
                {
                    BodyCell(table.Cell().ColumnSpan(5), "Balance brought forward");
                    BodyCell(table.Cell(), Bal(s.OpeningBalance), true);
                }
                foreach (var e in s.Entries)
                {
                    BodyCell(table.Cell(), D(e.Date));
                    BodyCell(table.Cell(), e.Reference);
                    BodyCell(table.Cell(), e.Description);
                    BodyCell(table.Cell(), e.Debit == 0 ? "" : N(e.Debit), true);
                    BodyCell(table.Cell(), e.Credit == 0 ? "" : N(e.Credit), true);
                    BodyCell(table.Cell(), Bal(e.Balance), true);
                }
            });
            Totals(col, [("Total charged", N(s.TotalDebits), false), ("Total paid", N(s.TotalCredits), false),
                (s.ClosingBalance >= 0 ? "Amount owing (KES)" : "In credit (KES)", Bal(s.ClosingBalance), true)]);
        });
    }

    // ------------------------------------------------------------------ remittance

    public byte[] Remittance(RemittanceDetails details, BusinessProfile business)
    {
        var r = details.Remittance;
        var meta = new List<(string, string)>
        {
            ("Statement no.", r.RemittanceNumber),
            ("Month", r.PeriodStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture)),
            ("Contract", r.Contract.ContractNumber),
            ("Status", r.Status.ToString())
        };

        return Render(business, "OWNER STATEMENT", meta, col =>
        {
            var o = r.Owner;
            var payTo = o.PayoutMethod switch
            {
                PayoutMethod.Bank => $"{o.BankName}, account {o.BankAccountNumber}",
                PayoutMethod.Mpesa => $"M-PESA {KenyanPhone.Display(o.MpesaNumber)}",
                _ => "Cheque"
            };
            BilledTo(col, "Prepared for", o.FullName, $"Paid to: {payTo}", null);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(5); c.RelativeColumn(2); });
                table.Header(h => { HeaderCell(h.Cell(), "Details"); HeaderCell(h.Cell(), "Amount (KES)", true); });
                foreach (var l in details.Lines)
                {
                    BodyCell(table.Cell(), l.Description);
                    BodyCell(table.Cell(), l.Amount < 0 ? $"({N(-l.Amount)})" : N(l.Amount), true);
                }
            });

            var totals = new List<(string, string, bool)> { ("Collected", N(r.GrossCollections), false), ("Commission", $"({N(r.CommissionAmount)})", false) };
            if (r.AdjustmentsTotal != 0) totals.Add(("Adjustments", r.AdjustmentsTotal < 0 ? $"({N(-r.AdjustmentsTotal)})" : N(r.AdjustmentsTotal), false));
            totals.Add(("Amount payable to you", N(r.NetPayable), true));
            Totals(col, totals);

            if (r.DepositsExcluded > 0)
                col.Item().Text($"Tenant deposits of KES {N(r.DepositsExcluded)} received this month are held by the agency and not included.").FontColor(Muted);
            if (r.Status == RemittanceStatus.Paid)
                col.Item().Text($"Paid on {D(r.PaidOn!.Value)} by {r.PaidMethod}{(r.PaidReference is null ? "" : $", reference {r.PaidReference}")}.").SemiBold();
            else
                col.Item().Text($"Due by {D(details.DueBy)}.").FontColor(Muted);
        });
    }

    // ------------------------------------------------------------------ sale statement

    public byte[] SaleStatement(SaleDetails details, BusinessProfile business)
    {
        var sale = details.Sale;
        var meta = new List<(string, string)>
        {
            ("Sale no.", sale.SaleNumber),
            ("Property", $"{sale.Unit.UnitNumber}, {sale.Unit.Asset.Name}"),
            ("Sale date", D(sale.SaleDate)),
            ("Status", sale.Status.ToString())
        };

        return Render(business, "BUYER STATEMENT", meta, col =>
        {
            BilledTo(col, "Prepared for", sale.Buyer.FullName, KenyanPhone.Display(sale.Buyer.Phone), sale.Buyer.Email);

            col.Item().Text("Payment schedule").SemiBold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1.6f); c.RelativeColumn(1.6f); c.RelativeColumn(1.6f); c.RelativeColumn(1.4f); });
                table.Header(h => { HeaderCell(h.Cell(), "Item"); HeaderCell(h.Cell(), "Due"); HeaderCell(h.Cell(), "Amount", true); HeaderCell(h.Cell(), "Paid", true); HeaderCell(h.Cell(), "Status"); });
                foreach (var i in details.Schedule)
                {
                    BodyCell(table.Cell(), i.Description);
                    BodyCell(table.Cell(), D(i.DueDate));
                    BodyCell(table.Cell(), N(i.Amount), true);
                    BodyCell(table.Cell(), N(i.AmountPaid), true);
                    BodyCell(table.Cell(), i.State switch { InstalmentState.PartlyPaid => "Part paid", var x => x.ToString() });
                }
            });

            var received = details.Payments.Where(p => p.Status == PaymentStatus.Active).ToList();
            if (received.Count > 0)
            {
                col.Item().Text("Payments received").SemiBold();
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c => { c.RelativeColumn(1.6f); c.RelativeColumn(1.6f); c.RelativeColumn(3); c.RelativeColumn(1.6f); });
                    table.Header(h => { HeaderCell(h.Cell(), "Date"); HeaderCell(h.Cell(), "Receipt"); HeaderCell(h.Cell(), "Method"); HeaderCell(h.Cell(), "Amount", true); });
                    foreach (var p in received)
                    {
                        BodyCell(table.Cell(), D(p.PaymentDate));
                        BodyCell(table.Cell(), p.ReceiptNumber);
                        BodyCell(table.Cell(), Method(p.Method) + (p.Reference is null ? "" : $" {p.Reference}"));
                        BodyCell(table.Cell(), N(p.Amount), true);
                    }
                });
            }

            var totals = new List<(string, string, bool)> { ("Price", N(sale.Price), false), ("Paid", N(sale.AmountPaid), false) };
            if (details.OverdueAmount > 0) totals.Add(("Overdue now", N(details.OverdueAmount), false));
            totals.Add(("Balance (KES)", N(sale.Price - sale.AmountPaid), true));
            Totals(col, totals);

            var done = details.Milestones.Where(m => m.CompletedOn != null).ToList();
            if (done.Count > 0)
                col.Item().Text("Completed: " + string.Join("; ", done.Select(m => $"{m.Type} on {D(m.CompletedOn!.Value)}"))).FontColor(Muted);
        });
    }

    // ------------------------------------------------------------------ layout helpers

    private static byte[] Render(BusinessProfile business, string title, IReadOnlyList<(string Label, string Value)> meta, Action<ColumnDescriptor> body)
        => Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(x => x.FontSize(9.5f).FontColor(Ink));

            page.Header().PaddingBottom(14).BorderBottom(1.5f).BorderColor(Ink).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    if (business.Logo is { Length: > 0 } logo)
                        c.Item().PaddingBottom(4).Height(42).AlignLeft().Image(logo).FitHeight();
                    c.Item().Text(business.Name).FontSize(16).Bold().FontColor(Brand);
                    if (business.Address is not null) c.Item().Text(business.Address).FontColor(Muted);
                    var contact = string.Join("  |  ", new[] { KenyanPhone.Display(business.Phone), business.Email }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (contact.Length > 0) c.Item().Text(contact).FontColor(Muted);
                    if (business.KraPin is not null) c.Item().Text($"KRA PIN: {business.KraPin}").FontColor(Muted);
                });
                row.ConstantItem(200).Column(c =>
                {
                    c.Item().AlignRight().Text(title).FontSize(13).Bold();
                    foreach (var (label, value) in meta)
                        c.Item().AlignRight().Text(t => { t.Span($"{label}: ").FontColor(Muted); t.Span(value).SemiBold(); });
                });
            });

            page.Content().PaddingVertical(14).Column(col =>
            {
                col.Spacing(12);
                body(col);
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                t.Span("Generated by ShambaNyumba  |  Page ");
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        })).GeneratePdf();

    private static void BilledTo(ColumnDescriptor col, string label, string name, string? line2, string? line3)
        => col.Item().Column(c =>
        {
            c.Item().Text(label).FontColor(Muted).FontSize(8.5f);
            c.Item().Text(name).Bold();
            if (!string.IsNullOrWhiteSpace(line2)) c.Item().Text(line2);
            if (!string.IsNullOrWhiteSpace(line3)) c.Item().Text(line3);
        });

    private static void HeaderCell(IContainer cell, string text, bool right = false)
    {
        var c = cell.BorderBottom(1).BorderColor(Ink).PaddingVertical(4);
        (right ? c.AlignRight() : c).Text(text).SemiBold();
    }

    private static void BodyCell(IContainer cell, string text, bool right = false)
    {
        var c = cell.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4);
        (right ? c.AlignRight() : c).Text(text);
    }

    private static void Totals(ColumnDescriptor col, IReadOnlyList<(string Label, string Value, bool Strong)> rows)
        => col.Item().AlignRight().Width(260).Column(c =>
        {
            foreach (var (label, value, strong) in rows)
                c.Item().PaddingVertical(2).Row(r =>
                {
                    var l = r.RelativeItem().AlignRight().PaddingRight(10).Text(label);
                    var v = r.ConstantItem(110).AlignRight().Text(value);
                    if (strong) { l.Bold(); v.Bold().FontSize(11); }
                });
        });

    private static string Method(PaymentMethod m) => m switch
    {
        PaymentMethod.Mpesa => "M-PESA",
        PaymentMethod.BankTransfer => "Bank transfer",
        _ => m.ToString()
    };
}
