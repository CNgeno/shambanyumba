using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Depends on: DbContext, INumberSequenceService (low-level), TimeProvider.
/// It changes Unit.Status directly (Occupied on activation, Available/Inactive on ending) in the SAME
/// SaveChanges as the lease, so a unit and its lease can never disagree. It never calls UnitService.
/// </summary>
public sealed class LeaseService(
    ApplicationDbContext db,
    INumberSequenceService numbers,
    TimeProvider clock) : ILeaseService
{
    private DateOnly Today => clock.TodayEat();

    // ------------------------------------------------------------------ queries

    public async Task<IReadOnlyList<LeaseSummary>> ListAsync(LeaseListFilter filter, CancellationToken ct = default)
    {
        var query = db.Leases.AsNoTracking();

        query = filter.View switch
        {
            LeaseListView.Active => query.Where(l => l.Status == LeaseStatus.Active),
            LeaseListView.EndingSoon => query.Where(l => l.Status == LeaseStatus.Active && l.EndDate != null && l.EndDate <= Today.AddDays(90)),
            LeaseListView.Draft => query.Where(l => l.Status == LeaseStatus.Draft),
            LeaseListView.Ended => query.Where(l => l.Status == LeaseStatus.Expired || l.Status == LeaseStatus.Terminated || l.Status == LeaseStatus.Renewed),
            _ => query
        };
        if (filter.AssetId is int assetId)
            query = query.Where(l => l.Unit.AssetId == assetId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(l => l.LeaseNumber.Contains(term) || l.Tenant.FullName.Contains(term) || l.Unit.UnitNumber.Contains(term));
        }

        var rows = await Project(query).ToListAsync(ct);
        var ordered = filter.View == LeaseListView.EndingSoon
            ? rows.OrderBy(r => r.EndDate)
            : rows.OrderBy(r => r.AssetName).ThenBy(r => r.UnitNumber, NaturalComparer.Instance);
        return ordered.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyList<LeaseSummary>> GetNeedingAttentionAsync(int withinDays, CancellationToken ct = default)
    {
        var limit = Today.AddDays(withinDays);
        var rows = await Project(db.Leases.AsNoTracking()
                .Where(l => l.Status == LeaseStatus.Active && l.EndDate != null && l.EndDate <= limit))
            .ToListAsync(ct);
        return rows.OrderBy(r => r.EndDate).Select(ToSummary).ToList();
    }

    public async Task<LeaseCounts> GetCountsAsync(CancellationToken ct = default)
    {
        var today = Today;
        var soon = today.AddDays(90);
        var rows = await db.Leases.AsNoTracking()
            .Where(l => l.Status == LeaseStatus.Active || l.Status == LeaseStatus.Draft)
            .Select(l => new { l.Status, l.EndDate })
            .ToListAsync(ct);
        return new LeaseCounts(
            rows.Count(r => r.Status == LeaseStatus.Active),
            rows.Count(r => r.Status == LeaseStatus.Active && r.EndDate >= today && r.EndDate <= soon),
            rows.Count(r => r.Status == LeaseStatus.Active && r.EndDate < today),
            rows.Count(r => r.Status == LeaseStatus.Draft));
    }

    public async Task<LeaseDetails?> GetDetailsAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await db.Leases.AsNoTracking()
            .Include(l => l.Tenant)
            .Include(l => l.Unit).ThenInclude(u => u.Asset)
            .FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return null;

        var from = lease.RenewedFromLeaseId is int fromId
            ? await Project(db.Leases.AsNoTracking().Where(l => l.Id == fromId)).FirstOrDefaultAsync(ct)
            : null;
        var to = await Project(db.Leases.AsNoTracking().Where(l => l.RenewedFromLeaseId == leaseId)).FirstOrDefaultAsync(ct);

        var docs = await db.TenantDocuments.AsNoTracking()
            .Where(d => d.LeaseId == leaseId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new TenantDocumentSummary(d.Id, d.TenantId, d.LeaseId, d.DocumentType, d.Title, d.FileName, d.SizeBytes, d.CreatedAtUtc))
            .ToListAsync(ct);

        return new LeaseDetails(lease, LeaseRules.Expiry(lease.Status, lease.EndDate, Today),
            from is null ? null : ToSummary(from), to is null ? null : ToSummary(to), docs);
    }

    public async Task<IReadOnlyList<LettableUnit>> GetLettableUnitsAsync(int? alsoIncludeUnitId, CancellationToken ct = default)
    {
        var units = await db.Units.AsNoTracking()
            .Where(u => u.Status == UnitStatus.Available || u.Status == UnitStatus.Reserved || u.Id == alsoIncludeUnitId)
            .Select(u => new { u.Id, u.UnitNumber, u.AssetId, AssetName = u.Asset.Name, NodeName = u.StructureNode != null ? u.StructureNode.Name : null, u.MonthlyRent, u.Status })
            .ToListAsync(ct);

        return units
            .OrderBy(u => u.AssetName).ThenBy(u => u.UnitNumber, NaturalComparer.Instance)
            .Select(u => new LettableUnit(u.Id, u.UnitNumber, u.AssetId, u.AssetName, u.NodeName, u.MonthlyRent, u.Status))
            .ToList();
    }

    // ------------------------------------------------------------------ commands

    public async Task<Result<int>> CreateAsync(LeaseInput input, bool activate, CancellationToken ct = default)
    {
        var error = await ValidateAsync(input, null, ct);
        if (error is not null) return Result<int>.Fail(error);

        var number = await numbers.NextAsync(SequenceKeys.Lease, ct);   // before adding anything else
        var lease = new Lease { LeaseNumber = $"L-{number:00000}", Status = LeaseStatus.Draft };
        Apply(lease, input);
        db.Leases.Add(lease);
        // The estate's charges come with every new lease (remove or change them on the lease if this one differs).
        foreach (var charge in await EstateService.ChargesForNewLeaseAsync(db, input.UnitId, ct))
        {
            charge.Lease = lease;
            db.LeaseCharges.Add(charge);
        }
        await db.SaveChangesAsync(ct);

        if (!activate) return Result<int>.Ok(lease.Id);

        var activated = await ActivateAsync(lease.Id, ct);
        // Failure still carries the draft's id, so the caller can send the user to it.
        return activated.Succeeded
            ? Result<int>.Ok(lease.Id)
            : new Result<int>(false, $"Saved as draft {lease.LeaseNumber}, but it couldn't be activated: {activated.Error}", lease.Id);
    }

    public async Task<Result> UpdateDraftAsync(int leaseId, LeaseInput input, CancellationToken ct = default)
    {
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result.Fail("Lease not found.");
        if (lease.Status != LeaseStatus.Draft)
            return Result.Fail("Only draft leases can be edited. To change the terms of an active lease, renew it.");

        var error = await ValidateAsync(input, leaseId, ct);
        if (error is not null) return Result.Fail(error);

        Apply(lease, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> DeleteDraftAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result.Fail("Lease not found.");
        if (lease.Status != LeaseStatus.Draft) return Result.Fail("Only draft leases can be deleted.");

        var charges = await db.LeaseCharges.Where(c => c.LeaseId == leaseId).ToListAsync(ct);
        db.LeaseCharges.RemoveRange(charges);
        db.Leases.Remove(lease);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ActivateAsync(int leaseId, CancellationToken ct = default)
    {
        var lease = await db.Leases.Include(l => l.Unit).FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result.Fail("Lease not found.");

        var error = LeaseRules.ActivationError(lease.Status, lease.Unit.Status);
        if (error is not null) return Result.Fail(error);

        lease.Status = LeaseStatus.Active;
        lease.ActivatedAtUtc = clock.GetUtcNow().UtcDateTime;
        lease.Unit.Status = UnitStatus.Occupied;

        try
        {
            await db.SaveChangesAsync(ct);   // lease + unit together
        }
        catch (DbUpdateException)
        {
            // UX_Leases_OneActivePerUnit, or the unit changed under us.
            return Result.Fail("This unit already has an active lease.");
        }
        return Result.Ok();
    }

    public async Task<Result<int>> RenewAsync(int leaseId, LeaseRenewalInput input, CancellationToken ct = default)
    {
        var old = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (old is null) return Result<int>.Fail("Lease not found.");
        if (old.Status != LeaseStatus.Active) return Result<int>.Fail("Only active leases can be renewed.");
        if (input.StartDate <= old.StartDate) return Result<int>.Fail("The renewal must start after the current lease started.");

        var error = LeaseRules.ValidateTerms(input.StartDate, input.EndDate, input.MonthlyRent, input.DepositAmount, input.NoticePeriodDays, input.RentDueDay);
        if (error is not null) return Result<int>.Fail(error);

        var number = await numbers.NextAsync(SequenceKeys.Lease, ct);

        // Two saves in one transaction: the old lease must stop being Active before the new one starts,
        // otherwise the one-active-lease-per-unit index would reject the insert.
        return await db.InTransactionAsync(async () =>
        {
            var current = await db.Leases.FirstAsync(l => l.Id == leaseId, ct);
            current.Status = LeaseStatus.Renewed;
            current.EndedOn = input.StartDate.AddDays(-1);
            await db.SaveChangesAsync(ct);

            var renewal = new Lease
            {
                LeaseNumber = $"L-{number:00000}",
                TenantId = current.TenantId,
                UnitId = current.UnitId,
                Status = LeaseStatus.Active,
                StartDate = input.StartDate,
                EndDate = input.EndDate,
                MonthlyRent = input.MonthlyRent,
                DepositAmount = input.DepositAmount,
                NoticePeriodDays = input.NoticePeriodDays,
                RentDueDay = input.RentDueDay,
                Notes = input.Notes?.Trim(),
                ActivatedAtUtc = clock.GetUtcNow().UtcDateTime,
                RenewedFromLeaseId = current.Id
            };
            db.Leases.Add(renewal);

            // Charges (garbage, security, water...) carry over to the renewal.
            var charges = await db.LeaseCharges.AsNoTracking().Where(c => c.LeaseId == current.Id).ToListAsync(ct);
            foreach (var c in charges)
                db.LeaseCharges.Add(new LeaseCharge
                {
                    Lease = renewal, ChargeDefinitionId = c.ChargeDefinitionId,
                    AmountOverride = c.AmountOverride, UnitRateOverride = c.UnitRateOverride
                });

            await db.SaveChangesAsync(ct);   // unit stays Occupied throughout
            return Result<int>.Ok(renewal.Id);
        }, ct);
    }

    public async Task<Result> TerminateAsync(int leaseId, LeaseTerminationInput input, CancellationToken ct = default)
    {
        var lease = await db.Leases.Include(l => l.Unit).FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result.Fail("Lease not found.");
        if (lease.Status != LeaseStatus.Active) return Result.Fail("Only active leases can be terminated.");
        if (input.EndDate < lease.StartDate) return Result.Fail("The end date can't be before the lease started.");
        if (input.NoticeGivenOn is DateOnly n && n > input.EndDate) return Result.Fail("Notice can't be given after the end date.");
        if (string.IsNullOrWhiteSpace(input.Reason)) return Result.Fail("Give a reason for ending the lease.");
        if (input.Penalty < 0 || input.DepositDeductions < 0) return Result.Fail("Amounts can't be negative.");
        if (input.DepositDeductions > lease.DepositAmount)
            return Result.Fail("Deductions can't be more than the deposit held. Bill anything extra separately.");

        lease.Status = LeaseStatus.Terminated;
        lease.EndedOn = input.EndDate;
        lease.NoticeGivenOn = input.NoticeGivenOn;
        lease.TerminationReason = input.Reason.Trim();
        lease.TerminationPenalty = input.Penalty;
        lease.DepositDeductions = input.DepositDeductions;
        lease.DepositNote = input.DepositNote?.Trim();
        lease.Unit.Status = input.MakeUnitInactive ? UnitStatus.Inactive : UnitStatus.Available;

        // Sprint 4/5: the penalty becomes an invoice line and the deposit refund a payment-out.
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> EndAtExpiryAsync(int leaseId, bool makeUnitInactive, CancellationToken ct = default)
    {
        var lease = await db.Leases.Include(l => l.Unit).FirstOrDefaultAsync(l => l.Id == leaseId, ct);
        if (lease is null) return Result.Fail("Lease not found.");
        if (lease.Status != LeaseStatus.Active) return Result.Fail("Only active leases can be ended.");
        if (lease.EndDate is not DateOnly end)
            return Result.Fail("This lease has no end date. Use Terminate to record when the tenant leaves.");
        if (end > Today)
            return Result.Fail($"This lease runs until {end:d MMM yyyy}. To end it early, use Terminate.");

        lease.Status = LeaseStatus.Expired;
        lease.EndedOn = end;
        lease.DepositDeductions ??= 0;
        lease.Unit.Status = makeUnitInactive ? UnitStatus.Inactive : UnitStatus.Available;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ helpers

    private sealed record LeaseRow(
        int Id, string LeaseNumber, int TenantId, string TenantName, int UnitId, string UnitNumber,
        int AssetId, string AssetName, DateOnly StartDate, DateOnly? EndDate, decimal MonthlyRent, LeaseStatus Status);

    private static IQueryable<LeaseRow> Project(IQueryable<Lease> query)
        => query.Select(l => new LeaseRow(l.Id, l.LeaseNumber, l.TenantId, l.Tenant.FullName, l.UnitId, l.Unit.UnitNumber,
            l.Unit.AssetId, l.Unit.Asset.Name, l.StartDate, l.EndDate, l.MonthlyRent, l.Status));

    private LeaseSummary ToSummary(LeaseRow r)
        => new(r.Id, r.LeaseNumber, r.TenantId, r.TenantName, r.UnitId, r.UnitNumber, r.AssetId, r.AssetName,
            r.StartDate, r.EndDate, r.MonthlyRent, r.Status, LeaseRules.Expiry(r.Status, r.EndDate, Today));

    private async Task<string?> ValidateAsync(LeaseInput input, int? leaseId, CancellationToken ct)
    {
        var error = LeaseRules.ValidateTerms(input.StartDate, input.EndDate, input.MonthlyRent, input.DepositAmount, input.NoticePeriodDays, input.RentDueDay);
        if (error is not null) return error;

        if (!await db.Tenants.AnyAsync(t => t.Id == input.TenantId, ct)) return "Choose a tenant.";

        var unit = await db.Units.AsNoTracking().Where(u => u.Id == input.UnitId).Select(u => new { u.Status }).FirstOrDefaultAsync(ct);
        if (unit is null) return "Choose a unit.";
        if (unit.Status is UnitStatus.Sold or UnitStatus.Inactive)
            return "That unit is sold or inactive and can't be leased.";

        // Only one draft at a time per unit avoids two people preparing competing leases.
        if (await db.Leases.AnyAsync(l => l.UnitId == input.UnitId && l.Status == LeaseStatus.Draft && l.Id != (leaseId ?? 0), ct))
            return "There's already a draft lease for this unit. Open it from Leases > Drafts.";

        return null;
    }

    private static void Apply(Lease lease, LeaseInput input)
    {
        lease.TenantId = input.TenantId;
        lease.UnitId = input.UnitId;
        lease.StartDate = input.StartDate;
        lease.EndDate = input.EndDate;
        lease.MonthlyRent = input.MonthlyRent;
        lease.DepositAmount = input.DepositAmount;
        lease.NoticePeriodDays = input.NoticePeriodDays;
        lease.RentDueDay = input.RentDueDay;
        lease.Notes = input.Notes?.Trim();
    }
}
