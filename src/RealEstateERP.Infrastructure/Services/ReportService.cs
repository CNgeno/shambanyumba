using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-13 business reports. Read-only. Depends on: DbContext, TimeProvider.
/// Invoiced = issued invoices dated in the month (cancelled excluded); collected = payments received in the month
/// (reversed excluded). Every query aggregates in SQL or groups after projecting to plain values.
/// </summary>
public sealed class ReportService(ApplicationDbContext db, TimeProvider clock) : IReportService
{
    public async Task<BusinessReports> GetAsync(int months, CancellationToken ct = default)
    {
        months = Math.Clamp(months, 1, 36);
        var today = clock.TodayEat();
        var from = new DateOnly(today.Year, today.Month, 1).AddMonths(-(months - 1));

        // ---- collections by month ----
        var invoiced = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft && i.InvoiceDate >= from && i.InvoiceDate <= today)
            .GroupBy(i => new { i.InvoiceDate.Year, i.InvoiceDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(i => i.Total) }).ToListAsync(ct);
        var collected = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Active && p.PaymentDate >= from && p.PaymentDate <= today)
            .GroupBy(p => new { p.PaymentDate.Year, p.PaymentDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var collections = Enumerable.Range(0, months).Select(i => from.AddMonths(i)).Select(m => new MonthCollections(m,
            invoiced.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0,
            collected.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0)).ToList();

        // ---- revenue by property and by unit type ----
        var invoiceRows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft && i.InvoiceDate >= from && i.InvoiceDate <= today)
            .Select(i => new
            {
                i.Total,
                AssetName = i.Lease != null ? i.Lease.Unit.Asset.Name : null,
                UnitType = i.Lease != null ? (UnitType?)i.Lease.Unit.Type : null
            }).ToListAsync(ct);
        var allocationRows = await db.PaymentAllocations.AsNoTracking()
            .Where(a => !a.IsReversed && a.Payment.Status == PaymentStatus.Active && a.Payment.PaymentDate >= from && a.Payment.PaymentDate <= today)
            .Select(a => new
            {
                a.Amount,
                AssetName = a.Invoice.Lease != null ? a.Invoice.Lease.Unit.Asset.Name : null,
                UnitType = a.Invoice.Lease != null ? (UnitType?)a.Invoice.Lease.Unit.Type : null
            }).ToListAsync(ct);
        var units = await db.Units.AsNoTracking().Where(u => u.Status != UnitStatus.Inactive)
            .Select(u => new { u.Id, u.UnitNumber, AssetId = u.AssetId, AssetName = u.Asset.Name, u.Type, u.Status, u.MonthlyRent, u.CreatedAtUtc })
            .ToListAsync(ct);

        var expenseRows = await db.ProjectCosts.AsNoTracking()
            .Where(c => c.Kind == CostKind.Actual && c.Category >= CostCategory.Letting && c.Date >= from && c.Date <= today)
            .Select(c => new { AssetName = c.Asset.Name, c.Amount }).ToListAsync(ct);
        const string other = "Not linked to a unit";
        var byProperty = invoiceRows.Select(r => r.AssetName ?? other).Concat(allocationRows.Select(r => r.AssetName ?? other)).Distinct()
            .Concat(expenseRows.Select(r => r.AssetName)).Distinct()
            .Select(name => new RevenueRow(name, units.Count(u => u.AssetName == name),
                invoiceRows.Where(r => (r.AssetName ?? other) == name).Sum(r => r.Total),
                allocationRows.Where(r => (r.AssetName ?? other) == name).Sum(r => r.Amount),
                expenseRows.Where(r => r.AssetName == name).Sum(r => r.Amount)))
            .OrderByDescending(r => r.Invoiced).ToList();
        var byUnitType = invoiceRows.Select(r => r.UnitType).Concat(allocationRows.Select(r => r.UnitType)).Distinct()
            .Select(t => new RevenueRow(t?.ToString() ?? other, t is UnitType ut ? units.Count(u => u.Type == ut) : 0,
                invoiceRows.Where(r => r.UnitType == t).Sum(r => r.Total),
                allocationRows.Where(r => r.UnitType == t).Sum(r => r.Amount)))
            .OrderByDescending(r => r.Invoiced).ToList();

        // ---- occupancy, turnover and vacancy ----
        var yearAgo = today.AddMonths(-12);
        var ended = await db.Leases.AsNoTracking()
            .Where(l => (l.Status == LeaseStatus.Terminated || l.Status == LeaseStatus.Expired) && l.EndedOn != null && l.EndedOn >= yearAgo)
            .Select(l => new { l.Unit.AssetId }).ToListAsync(ct);
        var rentable = units.Where(u => u.MonthlyRent > 0 || u.Status is UnitStatus.Occupied).ToList();
        var occupancy = rentable.GroupBy(u => (u.AssetId, u.AssetName))
            .Select(g => new OccupancyRow(g.Key.AssetId, g.Key.AssetName, g.Count(), g.Count(u => u.Status == UnitStatus.Occupied),
                g.Count(u => u.Status == UnitStatus.Available), ended.Count(e => e.AssetId == g.Key.AssetId)))
            .OrderBy(r => r.AssetName).ToList();

        var vacantIds = rentable.Where(u => u.Status == UnitStatus.Available).Select(u => u.Id).ToList();
        var lastEnded = await db.Leases.AsNoTracking()
            .Where(l => vacantIds.Contains(l.UnitId) && l.EndedOn != null)
            .GroupBy(l => l.UnitId).Select(g => new { UnitId = g.Key, Last = g.Max(l => l.EndedOn) })
            .ToDictionaryAsync(x => x.UnitId, x => x.Last, ct);
        var vacant = rentable.Where(u => u.Status == UnitStatus.Available)
            .Select(u =>
            {
                var since = lastEnded.TryGetValue(u.Id, out var d) && d is DateOnly endedOn ? endedOn : DateOnly.FromDateTime(u.CreatedAtUtc);
                return new VacantUnitRow(u.Id, u.UnitNumber, u.AssetName, u.MonthlyRent, since, Math.Max(0, today.DayNumber - since.DayNumber));
            })
            .OrderByDescending(v => v.DaysVacant).ToList();

        return new BusinessReports(collections, byProperty, byUnitType, occupancy, vacant, from, today);
    }

    public async Task<IReadOnlyDictionary<int, PropertyMoney>> GetPropertyMoneyAsync(CancellationToken ct = default)
    {
        var today = clock.TodayEat();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var invoiced = await db.Invoices.AsNoTracking()
            .Where(i => i.LeaseId != null && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Draft
                        && i.InvoiceDate >= monthStart && i.InvoiceDate <= monthEnd)
            .GroupBy(i => i.Lease!.Unit.AssetId)
            .Select(g => new { AssetId = g.Key, Total = g.Sum(i => i.Total) }).ToListAsync(ct);
        var collected = await db.PaymentAllocations.AsNoTracking()
            .Where(a => !a.IsReversed && a.Payment.Status == PaymentStatus.Active && a.Invoice.LeaseId != null
                        && a.Payment.PaymentDate >= monthStart && a.Payment.PaymentDate <= monthEnd)
            .GroupBy(a => a.Invoice.Lease!.Unit.AssetId)
            .Select(g => new { AssetId = g.Key, Total = g.Sum(a => a.Amount) }).ToListAsync(ct);
        var overdue = await db.Invoices.AsNoTracking()
            .Where(i => i.LeaseId != null && i.Status == InvoiceStatus.Pending && i.DueDate < today && i.Total > i.AmountPaid)
            .GroupBy(i => i.Lease!.Unit.AssetId)
            .Select(g => new { AssetId = g.Key, Total = g.Sum(i => i.Total - i.AmountPaid), Count = g.Count() }).ToListAsync(ct);

        var yearAgo = today.AddMonths(-12);
        var collected12 = await db.PaymentAllocations.AsNoTracking()
            .Where(a => !a.IsReversed && a.Payment.Status == PaymentStatus.Active && a.Invoice.LeaseId != null
                        && a.Payment.PaymentDate > yearAgo && a.Payment.PaymentDate <= today)
            .GroupBy(a => a.Invoice.Lease!.Unit.AssetId)
            .Select(g => new { AssetId = g.Key, Total = g.Sum(a => a.Amount) }).ToListAsync(ct);
        var expenses = await db.ProjectCosts.AsNoTracking()
            .Where(c => c.Kind == CostKind.Actual && c.Category >= CostCategory.Letting && c.Date > yearAgo && c.Date <= today)
            .Select(c => new { c.AssetId, c.Amount, c.Date }).ToListAsync(ct);

        return invoiced.Select(x => x.AssetId).Concat(collected.Select(x => x.AssetId)).Concat(overdue.Select(x => x.AssetId))
            .Concat(collected12.Select(x => x.AssetId)).Concat(expenses.Select(x => x.AssetId)).Distinct()
            .ToDictionary(id => id, id => new PropertyMoney(id,
                invoiced.FirstOrDefault(x => x.AssetId == id)?.Total ?? 0,
                collected.FirstOrDefault(x => x.AssetId == id)?.Total ?? 0,
                overdue.FirstOrDefault(x => x.AssetId == id)?.Total ?? 0,
                overdue.FirstOrDefault(x => x.AssetId == id)?.Count ?? 0,
                expenses.Where(x => x.AssetId == id && x.Date >= monthStart).Sum(x => x.Amount),
                collected12.FirstOrDefault(x => x.AssetId == id)?.Total ?? 0,
                expenses.Where(x => x.AssetId == id).Sum(x => x.Amount)));
    }
}

/// <summary>Platform admin figures. Read-only. Depends on: DbContext, ISubscriptionService (snapshots), ISystemConfigService, TimeProvider.</summary>
public sealed class PlatformReportService(ApplicationDbContext db, ISubscriptionService subscriptions, ISystemConfigService config, TimeProvider clock) : IPlatformReportService
{
    public async Task<PlatformReports> GetAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var fees = await db.SubscriptionPlans.AsNoTracking().ToDictionaryAsync(p => p.Code, p => p.MonthlyFee, ct);
        var businessIds = await db.Businesses.AsNoTracking().Select(b => b.Id).ToListAsync(ct);
        var graceDays = await config.GetIntAsync(Core.Constants.ConfigKeys.GraceDays, 3, ct);

        var snapshots = new List<SubscriptionSnapshot>(businessIds.Count);
        foreach (var id in businessIds) snapshots.Add(await subscriptions.GetSnapshotAsync(id, ct));

        var paying = snapshots.Where(s => !s.IsTrial && s.Status is SubscriptionStatus.Active or SubscriptionStatus.Grace).ToList();
        var byPlan = paying.GroupBy(s => s.PlanName)
            .Select(g => (Plan: g.Key, Businesses: g.Count(), Mrr: g.Sum(s => fees.GetValueOrDefault(s.PlanCode))))
            .OrderByDescending(x => x.Mrr).ToList();
        var churned = snapshots.Count(s => s.Status == SubscriptionStatus.Suspended && !s.IsTrial
            && s.PaidThroughUtc is DateTime end && end >= now.AddDays(-30 - graceDays));

        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);
        var newBiz = await db.Businesses.AsNoTracking().Where(b => b.CreatedAtUtc >= start)
            .GroupBy(b => new { b.CreatedAtUtc.Year, b.CreatedAtUtc.Month }).Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync(ct);
        var subRevenue = await db.SubscriptionPayments.AsNoTracking()
            .Where(p => p.Status == PaymentVerificationStatus.Approved && p.ReviewedAtUtc >= start)
            .GroupBy(p => new { p.ReviewedAtUtc!.Value.Year, p.ReviewedAtUtc!.Value.Month }).Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var creditSales = await db.CreditPurchases.AsNoTracking()
            .Where(p => p.Status == PaymentVerificationStatus.Approved && p.ReviewedAtUtc >= start)
            .GroupBy(p => new { p.ReviewedAtUtc!.Value.Year, p.ReviewedAtUtc!.Value.Month }).Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var used = await db.CreditTransactions.AsNoTracking()
            .Where(t => t.Type == CreditTransactionType.Usage && t.CreatedAtUtc >= start)
            .GroupBy(t => new { t.CreatedAtUtc.Year, t.CreatedAtUtc.Month }).Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(t => -t.Credits) }).ToListAsync(ct);

        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i)).Select(m => new PlatformMonth(DateOnly.FromDateTime(m),
            newBiz.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Count ?? 0,
            subRevenue.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0,
            creditSales.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0,
            used.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month)?.Total ?? 0)).ToList();

        return new PlatformReports(byPlan.Sum(x => x.Mrr),
            snapshots.Count(s => s.Status == SubscriptionStatus.Trial), snapshots.Count(s => s.Status == SubscriptionStatus.Active),
            snapshots.Count(s => s.Status == SubscriptionStatus.Grace), snapshots.Count(s => s.Status == SubscriptionStatus.Suspended),
            churned, months, byPlan);
    }
}
