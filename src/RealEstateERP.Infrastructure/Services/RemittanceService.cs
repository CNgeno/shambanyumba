using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// FR-10-C. Depends on: DbContext, INumberSequenceService, TimeProvider.
/// Collections = payment allocations (not reversed, payment dated in the month) on invoices of leases in the
/// contract's properties; the deposit share of each invoice is left out. Totals are snapshotted into the draft.
/// </summary>
public sealed class RemittanceService(ApplicationDbContext db, INumberSequenceService numbers, IAgentScopeService agentScope, TimeProvider clock) : IRemittanceService
{
    private const string PrincipalOnly = "Only the principal or an admin can do this.";

    /// <summary>Restricted agents only reach statements on their own contracts.</summary>
    private async Task<bool> MayTouchAsync(int remittanceId, CancellationToken ct)
    {
        var scope = await agentScope.GetAsync(ct);
        return !scope.IsRestricted || await db.OwnerRemittances.AnyAsync(r => r.Id == remittanceId && r.Contract.AgentId == scope.AgentId, ct);
    }

    public async Task<RemittancePreview?> PreviewAsync(int contractId, int year, int month, CancellationToken ct = default)
    {
        var scope = await agentScope.GetAsync(ct);
        var contract = await db.ManagementContracts.AsNoTracking().Include(c => c.Owner).Include(c => c.Agent)
            .FirstOrDefaultAsync(c => c.Id == contractId && (!scope.IsRestricted || c.AgentId == scope.AgentId), ct);
        if (contract is null) return null;

        var (start, end) = BillingRules.MonthBounds(year, month);
        var rows = await CollectionsAsync(contractId, start, end, ct);
        var income = rows.Sum(r => r.Income);
        var deposits = rows.Sum(r => r.DepositsExcluded);
        var commission = AgencyRules.Commission(contract.CommissionType, contract.CommissionPercent, contract.FixedMonthlyFee, income);
        var exists = await db.OwnerRemittances.AnyAsync(r => r.ContractId == contractId && r.PeriodStart == start && r.Status != RemittanceStatus.Cancelled, ct);

        return new RemittancePreview(contract, start, end, rows, income, deposits, commission, AgencyRules.NetPayable(income, commission, 0), exists);
    }

    public async Task<Result<int>> CreateAsync(int contractId, int year, int month, CancellationToken ct = default)
    {
        var preview = await PreviewAsync(contractId, year, month, ct);
        if (preview is null) return Result<int>.Fail("Contract not found.");
        if (preview.Contract.Status == ContractStatus.Draft) return Result<int>.Fail("Activate the contract first.");
        if (preview.AlreadyExists) return Result<int>.Fail("There's already a remittance for this contract and month.");
        if (preview.PeriodStart > clock.TodayEat()) return Result<int>.Fail("That month hasn't started yet.");

        var number = await numbers.NextAsync(SequenceKeys.Remittance, ct);
        var remittance = new OwnerRemittance
        {
            RemittanceNumber = $"REM-{number:00000}",
            ContractId = contractId,
            OwnerId = preview.Contract.OwnerId,
            PeriodStart = preview.PeriodStart,
            PeriodEnd = preview.PeriodEnd,
            GrossCollections = preview.Income,
            DepositsExcluded = preview.DepositsExcluded,
            CommissionAmount = preview.Commission
        };
        var order = 0;
        foreach (var p in preview.Properties)
            remittance.Lines.Add(new RemittanceLine { LineType = RemittanceLineType.Collections, AssetId = p.AssetId, Description = $"Rent and charges collected, {p.AssetName}", Amount = p.Income, SortOrder = order++ });
        var c = preview.Contract;
        remittance.Lines.Add(new RemittanceLine
        {
            LineType = RemittanceLineType.Commission, SortOrder = 1000, Amount = -preview.Commission,
            Description = c.CommissionType == CommissionType.FixedMonthly ? "Management fee (fixed)" : $"Management commission ({c.CommissionPercent:0.##}%)"
        });
        Recalculate(remittance);

        db.OwnerRemittances.Add(remittance);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result<int>.Fail("There's already a remittance for this contract and month."); }
        return Result<int>.Ok(remittance.Id);
    }

    public async Task<IReadOnlyList<RemittanceSummary>> ListAsync(DateOnly? month, RemittanceStatus? status, int? ownerId, CancellationToken ct = default)
    {
        var scope = await agentScope.GetAsync(ct);
        var query = db.OwnerRemittances.AsNoTracking();
        if (scope.IsRestricted) query = query.Where(r => r.Contract.AgentId == scope.AgentId);
        if (month is DateOnly m) query = query.Where(r => r.PeriodStart == new DateOnly(m.Year, m.Month, 1));
        if (status is RemittanceStatus s) query = query.Where(r => r.Status == s);
        if (ownerId is int o) query = query.Where(r => r.OwnerId == o);
        var rows = await query.OrderByDescending(r => r.PeriodStart).ThenBy(r => r.Owner.FullName)
            .Select(r => new { r.Id, r.RemittanceNumber, r.ContractId, r.Contract.ContractNumber, r.OwnerId, OwnerName = r.Owner.FullName,
                r.PeriodStart, r.GrossCollections, r.CommissionAmount, r.NetPayable, r.Status, r.Contract.RemittanceDay })
            .Take(500).ToListAsync(ct);
        return rows.Select(r => new RemittanceSummary(r.Id, r.RemittanceNumber, r.ContractId, r.ContractNumber, r.OwnerId, r.OwnerName,
            r.PeriodStart, r.GrossCollections, r.CommissionAmount, r.NetPayable, r.Status, DueBy(r.PeriodStart, r.RemittanceDay))).ToList();
    }

    public async Task<RemittanceDetails?> GetAsync(int id, CancellationToken ct = default)
    {
        if (!await MayTouchAsync(id, ct)) return null;
        var r = await db.OwnerRemittances.AsNoTracking().Include(x => x.Owner).Include(x => x.Contract).ThenInclude(c => c.Agent)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        var lines = await db.RemittanceLines.AsNoTracking().Where(l => l.RemittanceId == id).OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToListAsync(ct);
        return new RemittanceDetails(r, lines, DueBy(r.PeriodStart, r.Contract.RemittanceDay));
    }

    public async Task<Result> AddAdjustmentAsync(int id, string description, decimal amount, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description)) return Result.Fail("Describe the adjustment, e.g. plumbing repair at B2.");
        if (amount == 0) return Result.Fail("Enter an amount (negative to deduct from the owner).");
        if (!await MayTouchAsync(id, ct)) return Result.Fail("Remittance not found.");
        var r = await db.OwnerRemittances.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Remittance not found.");
        if (r.Status != RemittanceStatus.Draft) return Result.Fail("Only draft remittances can be changed.");

        r.Lines.Add(new RemittanceLine { LineType = RemittanceLineType.Adjustment, Description = description.Trim(), Amount = amount, SortOrder = 2000 + r.Lines.Count });
        Recalculate(r);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveAdjustmentAsync(int id, int lineId, CancellationToken ct = default)
    {
        if (!await MayTouchAsync(id, ct)) return Result.Fail("Remittance not found.");
        var r = await db.OwnerRemittances.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Remittance not found.");
        if (r.Status != RemittanceStatus.Draft) return Result.Fail("Only draft remittances can be changed.");
        var line = r.Lines.FirstOrDefault(l => l.Id == lineId && l.LineType == RemittanceLineType.Adjustment);
        if (line is null) return Result.Fail("Adjustment not found.");

        db.RemittanceLines.Remove(line);
        r.Lines.Remove(line);
        Recalculate(r);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ApproveAsync(int id, CancellationToken ct = default)
    {
        if ((await agentScope.GetAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var r = await db.OwnerRemittances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Remittance not found.");
        if (r.Status != RemittanceStatus.Draft) return Result.Fail("Only drafts can be approved.");
        if (r.NetPayable < 0) return Result.Fail("The amount payable is negative. Check the adjustments, or carry the shortfall to next month.");
        r.Status = RemittanceStatus.Approved;
        r.ApprovedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> MarkPaidAsync(int id, DateOnly paidOn, PayoutMethod method, string? reference, CancellationToken ct = default)
    {
        if ((await agentScope.GetAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        var r = await db.OwnerRemittances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Remittance not found.");
        if (r.Status != RemittanceStatus.Approved) return Result.Fail("Approve the remittance before recording the payment to the owner.");
        if (paidOn > clock.TodayEat()) return Result.Fail("The payment date can't be in the future.");
        if (method != PayoutMethod.Cheque && string.IsNullOrWhiteSpace(reference)) return Result.Fail("Enter the M-PESA or bank reference.");
        r.Status = RemittanceStatus.Paid;
        r.PaidOn = paidOn;
        r.PaidMethod = method;
        r.PaidReference = reference?.Trim().ToUpperInvariant();
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> CancelAsync(int id, string reason, CancellationToken ct = default)
    {
        if ((await agentScope.GetAsync(ct)).IsRestricted) return Result.Fail(PrincipalOnly);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Fail("Give a reason for cancelling.");
        var r = await db.OwnerRemittances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return Result.Fail("Remittance not found.");
        if (r.Status == RemittanceStatus.Paid) return Result.Fail("A paid remittance can't be cancelled. Correct it with an adjustment next month.");
        if (r.Status == RemittanceStatus.Cancelled) return Result.Fail("Already cancelled.");
        r.Status = RemittanceStatus.Cancelled;
        r.StatusReason = reason.Trim();
        await db.SaveChangesAsync(ct);   // the month can now be prepared again
        return Result.Ok();
    }

    public async Task<IReadOnlyList<AgentCommissionRow>> AgentCommissionsAsync(int year, int month, CancellationToken ct = default)
    {
        var start = new DateOnly(year, month, 1);
        var scope = await agentScope.GetAsync(ct);
        var rows = await db.OwnerRemittances.AsNoTracking()
            .Where(r => r.PeriodStart == start && r.Status != RemittanceStatus.Cancelled && (!scope.IsRestricted || r.Contract.AgentId == scope.AgentId))
            .Select(r => new { r.Contract.AgentId, AgentName = r.Contract.Agent != null ? r.Contract.Agent.FullName : null,
                Share = r.Contract.Agent != null ? r.Contract.Agent.CommissionSharePercent : 0m, r.CommissionAmount })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.AgentId)
            .Select(g =>
            {
                var commission = g.Sum(x => x.CommissionAmount);
                var share = g.First().Share;
                return new AgentCommissionRow(g.Key, g.First().AgentName ?? "No agent assigned", share, g.Count(), commission,
                    g.Key is null ? 0 : AgencyRules.AgentShare(commission, share));
            })
            .OrderByDescending(r => r.AgentShare).ThenBy(r => r.AgentName)
            .ToList();
    }

    // ---------------------------------------------------------------------

    private async Task<List<RemittancePropertyRow>> CollectionsAsync(int contractId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var links = await db.ContractProperties.AsNoTracking().Where(p => p.ContractId == contractId)
            .Select(p => new { p.AssetId, p.Asset.Name, p.AddedOn, p.RemovedOn }).ToListAsync(ct);
        var covered = links.Where(l => AgencyRules.CoveredDuring(l.AddedOn, l.RemovedOn, start, end)).ToList();
        var assetIds = covered.Select(l => l.AssetId).Distinct().ToList();
        if (assetIds.Count == 0) return [];

        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(a => !a.IsReversed && a.Payment.Status == PaymentStatus.Active
                        && a.Payment.PaymentDate >= start && a.Payment.PaymentDate <= end
                        && a.Invoice.Lease != null && assetIds.Contains(a.Invoice.Lease.Unit.AssetId))
            .Select(a => new { a.InvoiceId, a.Amount, a.Invoice.Total, AssetId = a.Invoice.Lease!.Unit.AssetId })
            .ToListAsync(ct);
        var invoiceIds = allocations.Select(a => a.InvoiceId).Distinct().ToList();
        var deposits = await db.InvoiceLines.AsNoTracking()
            .Where(l => invoiceIds.Contains(l.InvoiceId) && l.LineType == InvoiceLineType.Deposit)
            .GroupBy(l => l.InvoiceId).Select(g => new { InvoiceId = g.Key, Total = g.Sum(l => l.LineTotal) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Total, ct);

        return covered.GroupBy(l => l.AssetId).Select(g =>
        {
            var mine = allocations.Where(a => a.AssetId == g.Key).ToList();
            var collected = mine.Sum(a => a.Amount);
            var income = mine.Sum(a => AgencyRules.IncomePart(a.Amount, a.Total, deposits.GetValueOrDefault(a.InvoiceId)));
            return new RemittancePropertyRow(g.Key, g.First().Name, collected, collected - income, income);
        }).OrderBy(r => r.AssetName).ToList();
    }

    private static void Recalculate(OwnerRemittance r)
    {
        r.AdjustmentsTotal = r.Lines.Where(l => l.LineType == RemittanceLineType.Adjustment).Sum(l => l.Amount);
        r.NetPayable = AgencyRules.NetPayable(r.GrossCollections, r.CommissionAmount, r.AdjustmentsTotal);
    }

    private static DateOnly DueBy(DateOnly periodStart, int day) => periodStart.AddMonths(1).AddDays(Math.Clamp(day, 1, 28) - 1);
}
