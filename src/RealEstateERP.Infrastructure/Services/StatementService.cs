using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-04-C tenant statement, built on the fly from invoices, payments, reversals, write-offs and refunds,
/// so it can never disagree with them. Positive balance = tenant owes.
/// Debits: issued invoices, reversed payments, overpayment refunds. Credits: payments, write-offs.
/// Deposit refunds are listed for information only: the deposit was never part of the rent balance.
/// Depends on: DbContext only.
/// </summary>
public sealed class StatementService(ApplicationDbContext db, TimeProvider clock) : IStatementService
{
    public async Task<TenantStatement?> GetStatementAsync(int tenantId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return null;
        var end = to ?? clock.TodayEat();

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.WrittenOff))
            .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.PeriodStart, i.Total, i.AmountPaid, i.Status, i.StatusReason, i.UpdatedAtUtc, Unit = i.Lease != null ? i.Lease.Unit.UnitNumber : null })
            .ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Select(p => new { p.Id, p.ReceiptNumber, p.PaymentDate, p.Amount, p.Method, p.Reference, p.Status, p.ReversedAtUtc, p.ReversalReason })
            .ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .Select(r => new { r.Id, r.RefundNumber, r.RefundDate, r.Amount, r.Kind, r.PaymentId })
            .ToListAsync(ct);

        static DateOnly Eat(DateTime? utc) => DateOnly.FromDateTime((utc ?? DateTime.UtcNow).AddHours(3));

        // (date, order within a day, id) keeps the sequence stable: invoices before payments on the same day.
        var raw = new List<(DateOnly Date, int Order, int Id, StatementEntryType Type, string Ref, string Desc, decimal Debit, decimal Credit, string? Ctrl, int? LinkId)>();
        foreach (var i in invoices)
        {
            var label = i.PeriodStart is DateOnly p ? $"Invoice for {p:MMMM yyyy}" : "Invoice";
            raw.Add((i.InvoiceDate, 0, i.Id, StatementEntryType.Invoice, i.InvoiceNumber, label + (i.Unit is null ? "" : $", unit {i.Unit}"), i.Total, 0, "Invoices", i.Id));
            if (i.Status == InvoiceStatus.WrittenOff)
                raw.Add((Eat(i.UpdatedAtUtc), 3, i.Id, StatementEntryType.WriteOff, i.InvoiceNumber, $"Written off: {i.StatusReason}", 0, i.Total - i.AmountPaid, "Invoices", i.Id));
        }
        foreach (var p in payments)
        {
            var how = p.Method switch { PaymentMethod.Mpesa => "M-PESA", PaymentMethod.BankTransfer => "Bank transfer", _ => p.Method.ToString() };
            raw.Add((p.PaymentDate, 1, p.Id, StatementEntryType.Payment, p.ReceiptNumber, $"Payment, {how}{(p.Reference is null ? "" : $" {p.Reference}")}", 0, p.Amount, "Payments", p.Id));
            if (p.Status == PaymentStatus.Reversed)
                raw.Add((Eat(p.ReversedAtUtc), 2, p.Id, StatementEntryType.Reversal, p.ReceiptNumber, $"Payment reversed: {p.ReversalReason}", p.Amount, 0, "Payments", p.Id));
        }
        foreach (var r in refunds)
        {
            if (r.Kind == RefundKind.Overpayment)
                raw.Add((r.RefundDate, 4, r.Id, StatementEntryType.Refund, r.RefundNumber, "Overpayment refunded", r.Amount, 0, r.PaymentId is null ? null : "Payments", r.PaymentId));
            else
                raw.Add((r.RefundDate, 4, r.Id, StatementEntryType.DepositRefund, r.RefundNumber, $"Deposit refunded: KES {r.Amount:N2} (not part of the balance)", 0, 0, null, null));
        }

        var ordered = raw.Where(e => e.Date <= end).OrderBy(e => e.Date).ThenBy(e => e.Order).ThenBy(e => e.Id).ToList();
        var opening = from is DateOnly f ? ordered.Where(e => e.Date < f).Sum(e => e.Debit - e.Credit) : 0m;

        var balance = opening;
        var entries = new List<StatementEntry>();
        foreach (var e in ordered.Where(e => from is null || e.Date >= from))
        {
            balance += e.Debit - e.Credit;
            entries.Add(new StatementEntry(e.Date, e.Type, e.Ref, e.Desc, e.Debit, e.Credit, balance, e.Ctrl, e.LinkId));
        }
        return new TenantStatement(tenant, from, end, opening, entries, balance);
    }
}
