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
/// FR-09. Depends on: DbContext, INumberSequenceService, ISystemConfigService, TimeProvider.
/// Penalties are separate invoices (Kind = Penalty) pointing at the overdue invoice; UX_Invoices_OnePenaltyPerInvoice
/// guarantees one per invoice. No penalty is ever charged on a penalty.
/// </summary>
public sealed class ArrearsService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    ISystemConfigService config,
    TimeProvider clock,
    ICurrentUserService? currentUser = null) : IArrearsService
{
    private DateOnly Today => clock.TodayEat();

    // ------------------------------------------------------------------ aging report

    public async Task<ArrearsReport> GetReportAsync(int? assetId, CancellationToken ct = default)
    {
        var today = Today;
        var query = db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total);
        if (assetId is int a) query = query.Where(i => i.Lease != null && i.Lease.Unit.AssetId == a);

        var open = await query.Select(i => new
        {
            i.TenantId, TenantName = i.Tenant.FullName, i.Tenant.Phone, i.Tenant.CollectionStage, i.DueDate,
            Balance = i.Total - i.AmountPaid,
            AssetId = i.Lease != null ? (int?)i.Lease.Unit.AssetId : null,
            AssetName = i.Lease != null ? i.Lease.Unit.Asset.Name : null,
            UnitNumber = i.Lease != null ? i.Lease.Unit.UnitNumber : null
        }).ToListAsync(ct);

        var overdue = open.Where(o => o.DueDate < today).ToList();
        var notYetDue = open.Where(o => o.DueDate >= today).Sum(o => o.Balance);

        var totals = overdue.Aggregate(AgingTotals.Empty, (t, o) => t.Add(ArrearsRules.Bucket(ArrearsRules.DaysOverdue(o.DueDate, today)), o.Balance));

        var byProperty = overdue.GroupBy(o => (o.AssetId, o.AssetName))
            .Select(g => new PropertyArrears(g.Key.AssetId ?? 0, g.Key.AssetName ?? "Not linked to a property",
                g.Aggregate(AgingTotals.Empty, (t, o) => t.Add(ArrearsRules.Bucket(ArrearsRules.DaysOverdue(o.DueDate, today)), o.Balance)),
                g.Select(o => o.TenantId).Distinct().Count()))
            .OrderByDescending(p => p.Aging.Total)
            .ToList();

        var tenantIds = overdue.Select(o => o.TenantId).Distinct().ToList();
        var lastPayments = await db.Payments.AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId) && p.Status == PaymentStatus.Active)
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Last = g.Max(p => p.PaymentDate) })
            .ToDictionaryAsync(x => x.TenantId, x => x.Last, ct);
        var credits = await TenantCreditsAsync(tenantIds, ct);

        var tenants = overdue.GroupBy(o => o.TenantId)
            .Select(g =>
            {
                var first = g.First();
                return new TenantArrears(g.Key, first.TenantName, first.Phone,
                    string.Join(", ", g.Where(o => o.UnitNumber != null).Select(o => o.UnitNumber).Distinct()),
                    first.CollectionStage, g.Count(), g.Max(o => ArrearsRules.DaysOverdue(o.DueDate, today)),
                    g.Aggregate(AgingTotals.Empty, (t, o) => t.Add(ArrearsRules.Bucket(ArrearsRules.DaysOverdue(o.DueDate, today)), o.Balance)),
                    credits.GetValueOrDefault(g.Key), lastPayments.TryGetValue(g.Key, out var lp) ? lp : null);
            })
            .OrderByDescending(t => t.Aging.Over90).ThenByDescending(t => t.Aging.Total)
            .ToList();

        return new ArrearsReport(totals, notYetDue, byProperty, tenants);
    }

    // ------------------------------------------------------------------ penalties

    public async Task<IReadOnlyList<PenaltyCandidate>> PreviewPenaltiesAsync(CancellationToken ct = default)
    {
        var today = Today;
        // Precedence: the unit's own setting, then the business's (Business settings > Automation), then the system default.
        var business = currentUser?.BusinessId is int businessId
            ? await db.Businesses.AsNoTracking().Where(b => b.Id == businessId).Select(b => new { b.PenaltyGraceDays, b.PenaltyPercent }).FirstOrDefaultAsync(ct)
            : null;
        var defaultGrace = business?.PenaltyGraceDays ?? await config.GetIntAsync(ConfigKeys.DefaultGraceDaysAfterDue, 5, ct);
        var percent = business?.PenaltyPercent ?? await config.GetDecimalAsync(ConfigKeys.DefaultPenaltyPercent, 5m, ct);

        var penalised = db.Invoices.Where(p => p.PenaltyForInvoiceId != null && p.Status != InvoiceStatus.Cancelled).Select(p => p.PenaltyForInvoiceId!.Value);
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Pending && i.AmountPaid < i.Total && i.Kind != InvoiceKind.Penalty
                        && i.DueDate < today && !penalised.Contains(i.Id))
            .Select(i => new
            {
                i.Id, i.InvoiceNumber, i.TenantId, TenantName = i.Tenant.FullName, i.DueDate, Balance = i.Total - i.AmountPaid,
                UnitNumber = i.Lease != null ? i.Lease.Unit.UnitNumber : null,
                UnitGrace = i.Lease != null ? i.Lease.Unit.GracePeriodDays : null,
                UnitPenalty = i.Lease != null ? i.Lease.Unit.LatePenaltyAmount : null
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new { r, Grace = ArrearsRules.GraceDays(r.UnitGrace, defaultGrace) })
            .Where(x => ArrearsRules.PenaltyDue(x.r.DueDate, x.Grace, today))
            .Select(x => new PenaltyCandidate(x.r.Id, x.r.InvoiceNumber, x.r.TenantId, x.r.TenantName, x.r.UnitNumber, x.r.DueDate,
                ArrearsRules.DaysOverdue(x.r.DueDate, today), x.Grace, x.r.Balance,
                ArrearsRules.PenaltyAmount(x.r.Balance, x.r.UnitPenalty, percent), x.r.UnitPenalty is > 0))
            .Where(c => c.Penalty > 0)
            .OrderBy(c => c.TenantName).ThenBy(c => c.DueDate)
            .ToList();
    }

    public async Task<PenaltyRunResult> ApplyPenaltiesAsync(IReadOnlyCollection<int>? onlyInvoiceIds, CancellationToken ct = default)
    {
        var candidates = (await PreviewPenaltiesAsync(ct))
            .Where(c => onlyInvoiceIds is null || onlyInvoiceIds.Contains(c.InvoiceId))
            .ToList();
        var candidateIds = candidates.Select(c => c.InvoiceId).ToList();
        var leaseIds = await db.Invoices.AsNoTracking()
            .Where(i => candidateIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.LeaseId, ct);

        int created = 0;
        decimal total = 0;
        var problems = new List<string>();
        var today = Today;

        foreach (var c in candidates)
        {
            try
            {
                var number = await numbers.NextAsync(SequenceKeys.Invoice, ct);
                var penalty = new Invoice
                {
                    InvoiceNumber = InvoiceService.Format(number),
                    Kind = InvoiceKind.Penalty,
                    TenantId = c.TenantId,
                    LeaseId = leaseIds.GetValueOrDefault(c.InvoiceId),
                    PenaltyForInvoiceId = c.InvoiceId,
                    InvoiceDate = today,
                    DueDate = today.AddDays(7),
                    Status = InvoiceStatus.Pending,
                    IssuedAtUtc = clock.GetUtcNow().UtcDateTime,
                    Notes = $"Late payment penalty on {c.InvoiceNumber}."
                };
                penalty.Lines.Add(InvoiceComposer.Line(InvoiceLineType.Penalty,
                    $"Late payment penalty on {c.InvoiceNumber} (due {c.DueDate:d MMM yyyy}, {c.DaysOverdue} days late)", 1, c.Penalty, 0));
                InvoiceComposer.Recalculate(penalty);
                db.Invoices.Add(penalty);
                await db.SaveChangesAsync(ct);
                created++;
                total += penalty.Total;
            }
            catch (DbUpdateException)
            {
                problems.Add($"{c.InvoiceNumber}: already penalised by another run.");
            }
            finally
            {
                db.ChangeTracker.Clear();
            }
        }
        return new PenaltyRunResult(created, total, problems);
    }

    // ------------------------------------------------------------------ collections notes

    public async Task<IReadOnlyList<CollectionNoteSummary>> GetNotesAsync(int tenantId, CancellationToken ct = default)
        => await db.CollectionNotes.AsNoTracking()
            .Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Select(n => new CollectionNoteSummary(n.Id, n.CreatedAtUtc, n.CreatedBy, n.Stage, n.StageChanged, n.Note,
                n.PromisedPaymentDate, n.DocumentId, n.Document != null ? n.Document.FileName : null))
            .ToListAsync(ct);

    public async Task<Result> AddNoteAsync(CollectionNoteInput input, int? documentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Note)) return Result.Fail("Write what happened, e.g. called, promised to pay on Friday.");
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == input.TenantId, ct);
        if (tenant is null) return Result.Fail("Tenant not found.");
        if (documentId is int d && !await db.TenantDocuments.AnyAsync(x => x.Id == d && x.TenantId == input.TenantId, ct))
            return Result.Fail("That document doesn't belong to this tenant.");
        if (input.PromisedPaymentDate is DateOnly promised && promised < Today)
            return Result.Fail("A promised payment date can't be in the past.");

        var changed = tenant.CollectionStage != input.Stage;
        tenant.CollectionStage = input.Stage;
        db.CollectionNotes.Add(new CollectionNote
        {
            TenantId = tenant.Id, Stage = input.Stage, StageChanged = changed, Note = input.Note.Trim(),
            DocumentId = documentId, PromisedPaymentDate = input.PromisedPaymentDate
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------------

    private async Task<Dictionary<int, decimal>> TenantCreditsAsync(IReadOnlyCollection<int> tenantIds, CancellationToken ct)
    {
        var payments = await db.Payments.AsNoTracking()
            .Where(p => tenantIds.Contains(p.TenantId) && p.Status == PaymentStatus.Active)
            .Select(p => new { p.TenantId, Spare = p.Amount - (p.Allocations.Where(x => !x.IsReversed).Sum(x => (decimal?)x.Amount) ?? 0m) })
            .ToListAsync(ct);
        var refunds = await db.Refunds.AsNoTracking()
            .Where(r => tenantIds.Contains(r.TenantId) && r.Kind == RefundKind.Overpayment)
            .Select(r => new { r.TenantId, r.Amount }).ToListAsync(ct);
        return tenantIds.ToDictionary(id => id,
            id => Math.Max(0, payments.Where(p => p.TenantId == id).Sum(p => p.Spare) - refunds.Where(r => r.TenantId == id).Sum(r => r.Amount)));
    }
}
