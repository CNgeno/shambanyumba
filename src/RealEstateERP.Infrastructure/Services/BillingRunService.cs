using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Constants;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-07-A. Bills every active lease for a month: rent (first month prorated), deposit for brand-new leases,
/// recurring lease charges and unbilled meter readings.
/// Safe to run again: UX_Invoices_OneMonthlyPerLeasePeriod and the reading's BilledInvoiceId mean nothing is billed twice.
/// Each lease is saved separately, so one problem doesn't stop the rest.
/// Depends on: DbContext, INumberSequenceService, TimeProvider.
/// </summary>
public sealed class BillingRunService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    ICreditService credits,
    TimeProvider clock) : IBillingRunService
{
    private sealed record Plan(Lease Lease, string TenantName, string UnitNumber, string AssetName,
        bool AlreadyInvoiced, List<InvoiceLine> Lines, List<int> ReadingIds, DateOnly DueDate);

    public async Task<IReadOnlyList<BillingPreviewRow>> PreviewAsync(BillingRunRequest request, CancellationToken ct = default)
    {
        var plans = await BuildPlansAsync(request, ct);
        var warnings = await WarningsAsync(plans.Where(p => !p.AlreadyInvoiced).Select(p => p.Lease).ToList(), request, ct);
        return plans.Select(p => new BillingPreviewRow(p.Lease.Id, p.Lease.LeaseNumber, p.TenantName, p.UnitNumber, p.AssetName,
                p.AlreadyInvoiced, p.Lines.Count,
                p.Lines.Sum(l => BillingRules.LineMath(l.Quantity, l.UnitPrice, l.TaxPercent).Total),
                warnings.TryGetValue(p.Lease.Id, out var w) ? w : null))
            .ToList();
    }

    /// <summary>
    /// What an invoice would miss: an estate charge that isn't on the lease, or a metered estate charge with no
    /// reading for the unit in the month. Shown on the preview so nothing is billed short by accident.
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyList<PreviewWarning>>> WarningsAsync(List<Lease> leases, BillingRunRequest request, CancellationToken ct)
    {
        var result = new Dictionary<int, IReadOnlyList<PreviewWarning>>();
        if (leases.Count == 0) return result;
        var (_, periodEnd) = BillingRules.MonthBounds(request.Year, request.Month);
        var leaseIds = leases.Select(l => l.Id).ToList();
        var unitIds = leases.Select(l => l.UnitId).Distinct().ToList();

        var unitAsset = await db.Units.AsNoTracking().Where(u => unitIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.AssetId, ct);
        var assetIds = unitAsset.Values.Distinct().ToList();
        var estateCharges = await db.PropertyCharges.AsNoTracking().Where(p => assetIds.Contains(p.AssetId))
            .Select(p => new { p.AssetId, p.ChargeDefinitionId, p.ChargeDefinition.Name, p.ChargeDefinition.Type, p.ChargeDefinition.Frequency, p.ChargeDefinition.AnchorMonth })
            .ToListAsync(ct);
        if (estateCharges.Count == 0) return result;
        var onLeases = await db.LeaseCharges.AsNoTracking().Where(c => leaseIds.Contains(c.LeaseId))
            .Select(c => new { c.LeaseId, c.ChargeDefinitionId }).ToListAsync(ct);
        var readings = await db.UtilityMeterReadings.AsNoTracking()
            // The invoice picks up any unbilled reading dated by the month's end, so that's what to look for.
            .Where(r => unitIds.Contains(r.UnitId) && !r.IsOpening && r.BilledInvoiceId == null && r.ReadingDate <= periodEnd)
            .Select(r => new { r.UnitId, r.ChargeDefinitionId }).ToListAsync(ct);

        foreach (var lease in leases)
        {
            var assetId = unitAsset[lease.UnitId];
            var list = new List<PreviewWarning>();
            foreach (var c in estateCharges.Where(c => c.AssetId == assetId))
            {
                if (c.Type == ChargeType.Utility)
                {
                    if (!readings.Any(r => r.UnitId == lease.UnitId && r.ChargeDefinitionId == c.ChargeDefinitionId))
                        list.Add(new PreviewWarning($"No new {c.Name.ToLowerInvariant()} reading to bill", assetId, c.ChargeDefinitionId, false));
                }
                else if (!onLeases.Any(o => o.LeaseId == lease.Id && o.ChargeDefinitionId == c.ChargeDefinitionId)
                         && BillingRules.ChargeDue(c.Frequency, c.AnchorMonth, request.Month))
                {
                    list.Add(new PreviewWarning($"{c.Name} (an estate charge) isn't on this lease", assetId, c.ChargeDefinitionId, true));
                }
            }
            if (list.Count > 0) result[lease.Id] = list;
        }
        return result;
    }

    public async Task<BillingRunResult> GenerateAsync(BillingRunRequest request, CancellationToken ct = default)
    {
        var (periodStart, periodEnd) = BillingRules.MonthBounds(request.Year, request.Month);
        var plans = await BuildPlansAsync(request, ct);
        db.ChangeTracker.Clear();   // plans were read without tracking; start clean for NextAsync

        // FR-15: a run creating more than 10 invoices costs credits, charged up front.
        var toCreate = plans.Count(p => !p.AlreadyInvoiced && p.Lines.Count > 0);
        if (toCreate > CreditCosts.BulkInvoiceThreshold)
        {
            var charge = await credits.TryConsumeAsync(CreditCosts.BulkInvoiceRun, $"Invoice run, {periodStart:MMMM yyyy} ({toCreate} invoices)", null, ct);
            if (!charge.Succeeded) return new BillingRunResult(0, 0, 0, [charge.Error!]);
        }

        int created = 0, skipped = 0;
        decimal total = 0;
        var problems = new List<string>();

        foreach (var plan in plans)
        {
            if (plan.AlreadyInvoiced) { skipped++; continue; }
            if (plan.Lines.Count == 0) { problems.Add($"{plan.UnitNumber} ({plan.TenantName}): nothing to bill."); continue; }

            try
            {
                var number = await numbers.NextAsync(SequenceKeys.Invoice, ct);
                var invoice = new Invoice
                {
                    InvoiceNumber = InvoiceService.Format(number),
                    Kind = InvoiceKind.Monthly,
                    TenantId = plan.Lease.TenantId,
                    LeaseId = plan.Lease.Id,
                    InvoiceDate = clock.TodayEat(),
                    DueDate = plan.DueDate,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Status = request.IssueImmediately ? InvoiceStatus.Pending : InvoiceStatus.Draft,
                    IssuedAtUtc = request.IssueImmediately ? clock.GetUtcNow().UtcDateTime : null
                };
                foreach (var line in plan.Lines) invoice.Lines.Add(line);
                InvoiceComposer.Recalculate(invoice);
                db.Invoices.Add(invoice);

                if (plan.ReadingIds.Count > 0)
                {
                    var readings = await db.UtilityMeterReadings
                        .Where(r => plan.ReadingIds.Contains(r.Id) && r.BilledInvoiceId == null)
                        .ToListAsync(ct);
                    if (readings.Count != plan.ReadingIds.Count)
                        throw new DbUpdateException("A meter reading was billed by another run.");
                    foreach (var r in readings) r.BilledInvoice = invoice;
                }

                await db.SaveChangesAsync(ct);   // invoice, lines and readings together
                created++;
                total += invoice.Total;
            }
            catch (DbUpdateException)
            {
                skipped++;
                problems.Add($"{plan.UnitNumber} ({plan.TenantName}): already invoiced by another run.");
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }

        return new BillingRunResult(created, skipped, total, problems);
    }

    // ---------------------------------------------------------------------

    private async Task<List<Plan>> BuildPlansAsync(BillingRunRequest request, CancellationToken ct)
    {
        var (periodStart, periodEnd) = BillingRules.MonthBounds(request.Year, request.Month);

        var leaseQuery = db.Leases.AsNoTracking()
            .Where(l => l.Status == LeaseStatus.Active && l.StartDate <= periodEnd);
        if (request.AssetId is int assetId) leaseQuery = leaseQuery.Where(l => l.Unit.AssetId == assetId);

        var leases = await leaseQuery
            .Select(l => new { Lease = l, TenantName = l.Tenant.FullName, l.Unit.UnitNumber, AssetName = l.Unit.Asset.Name })
            .ToListAsync(ct);
        var leaseIds = leases.Select(l => l.Lease.Id).ToList();
        var unitIds = leases.Select(l => l.Lease.UnitId).ToList();

        var alreadyInvoiced = (await db.Invoices.AsNoTracking()
                .Where(i => i.Kind == InvoiceKind.Monthly && i.PeriodStart == periodStart && i.Status != InvoiceStatus.Cancelled
                            && i.LeaseId != null && leaseIds.Contains(i.LeaseId.Value))
                .Select(i => i.LeaseId!.Value).ToListAsync(ct))
            .ToHashSet();

        var depositInvoiced = (await db.InvoiceLines.AsNoTracking()
                .Where(l => l.LineType == InvoiceLineType.Deposit && l.Invoice.Status != InvoiceStatus.Cancelled
                            && l.Invoice.LeaseId != null && leaseIds.Contains(l.Invoice.LeaseId.Value))
                .Select(l => l.Invoice.LeaseId!.Value).ToListAsync(ct))
            .ToHashSet();

        var charges = await db.LeaseCharges.AsNoTracking()
            .Where(c => leaseIds.Contains(c.LeaseId) && c.ChargeDefinition.IsActive && c.ChargeDefinition.Type == ChargeType.Recurring)
            .Select(c => new { c.LeaseId, c.ChargeDefinitionId, c.ChargeDefinition.Name, c.ChargeDefinition.DefaultAmount, c.ChargeDefinition.TaxPercent, c.AmountOverride,
                c.ChargeDefinition.Frequency, c.ChargeDefinition.AnchorMonth })
            .ToListAsync(ct);
        var chargesByLease = charges.ToLookup(c => c.LeaseId);

        var readings = await db.UtilityMeterReadings.AsNoTracking()
            .Where(r => unitIds.Contains(r.UnitId) && r.BilledInvoiceId == null && !r.IsOpening && r.ReadingDate <= periodEnd && r.Amount > 0)
            .Select(r => new { r.Id, r.UnitId, r.ChargeDefinitionId, r.ChargeDefinition.Name, r.ChargeDefinition.UnitOfMeasure, r.ChargeDefinition.TaxPercent, r.Consumption, r.UnitRate, r.PreviousReading, r.CurrentReading, r.ReadingDate })
            .ToListAsync(ct);
        var readingsByUnit = readings.ToLookup(r => r.UnitId);

        var monthName = periodStart.ToString("MMMM yyyy");
        var plans = new List<Plan>(leases.Count);
        foreach (var row in leases.OrderBy(l => l.AssetName).ThenBy(l => l.UnitNumber, NaturalComparer.Instance))
        {
            var lease = row.Lease;
            var lines = new List<InvoiceLine>();
            var readingIds = new List<int>();

            var (rent, days, daysInPeriod) = BillingRules.RentForPeriod(lease.MonthlyRent, lease.StartDate, periodStart, periodEnd);
            if (rent > 0)
                lines.Add(InvoiceComposer.Line(InvoiceLineType.Rent,
                    days == daysInPeriod ? $"Rent, {monthName}" : $"Rent, {monthName} ({days} of {daysInPeriod} days from {lease.StartDate:d MMM})",
                    1, rent, 0));

            if (request.IncludeDeposits && BillingRules.ShouldInvoiceDeposit(lease.DepositAmount, lease.RenewedFromLeaseId != null,
                    lease.StartDate, periodStart, depositInvoiced.Contains(lease.Id)))
                lines.Add(InvoiceComposer.Line(InvoiceLineType.Deposit, "Security deposit (refundable)", 1, lease.DepositAmount, 0));

            foreach (var c in chargesByLease[lease.Id].Where(c => BillingRules.ChargeDue(c.Frequency, c.AnchorMonth, periodStart.Month)).OrderBy(c => c.Name))
            {
                var amount = c.AmountOverride ?? c.DefaultAmount ?? 0;
                var label = c.Frequency == ChargeFrequency.Monthly ? $"{c.Name}, {monthName}" : $"{c.Name} ({BillingRules.FrequencyLabel(c.Frequency)}), {monthName}";
                if (amount > 0)
                    lines.Add(InvoiceComposer.Line(InvoiceLineType.RecurringCharge, label, 1, amount, c.TaxPercent, c.ChargeDefinitionId));
            }

            foreach (var r in readingsByUnit[lease.UnitId].OrderBy(r => r.ReadingDate))
            {
                lines.Add(InvoiceComposer.Line(InvoiceLineType.Utility,
                    $"{r.Name}: {r.PreviousReading:0.###} to {r.CurrentReading:0.###} {r.UnitOfMeasure} (read {r.ReadingDate:d MMM})",
                    r.Consumption, r.UnitRate, r.TaxPercent, r.ChargeDefinitionId, r.Id));
                readingIds.Add(r.Id);
            }

            plans.Add(new Plan(lease, row.TenantName, row.UnitNumber, row.AssetName, alreadyInvoiced.Contains(lease.Id),
                lines, readingIds, BillingRules.DueDate(periodStart, lease.RentDueDay, lease.StartDate)));
        }
        return plans;
    }
}
