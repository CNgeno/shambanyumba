using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>Depends on: DbContext, TimeProvider. Never on invoice or lease services.</summary>
public sealed class ChargeService(ApplicationDbContext db, TimeProvider clock) : IChargeService
{
    // ------------------------------------------------------------------ definitions

    public async Task<IReadOnlyList<ChargeDefinition>> ListDefinitionsAsync(bool includeInactive, CancellationToken ct = default)
        => await db.ChargeDefinitions.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.Type).ThenBy(c => c.Name)
            .ToListAsync(ct);

    public Task<ChargeDefinition?> GetDefinitionAsync(int id, CancellationToken ct = default)
        => db.ChargeDefinitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Result<int>> CreateDefinitionAsync(ChargeDefinitionInput input, CancellationToken ct = default)
    {
        var error = await ValidateDefinitionAsync(input, null, ct);
        if (error is not null) return Result<int>.Fail(error);

        var def = new ChargeDefinition();
        Apply(def, input);
        db.ChargeDefinitions.Add(def);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(def.Id);
    }

    public async Task<Result> UpdateDefinitionAsync(int id, ChargeDefinitionInput input, CancellationToken ct = default)
    {
        var def = await db.ChargeDefinitions.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (def is null) return Result.Fail("Charge not found.");
        if (def.Type != input.Type && await db.LeaseCharges.AnyAsync(l => l.ChargeDefinitionId == id, ct))
            return Result.Fail("This charge is attached to leases, so its type can't change. Create a new charge instead.");

        var error = await ValidateDefinitionAsync(input, id, ct);
        if (error is not null) return Result.Fail(error);

        Apply(def, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ lease charges

    public async Task<IReadOnlyList<LeaseChargeSummary>> GetLeaseChargesAsync(int leaseId, CancellationToken ct = default)
        => (await db.LeaseCharges.AsNoTracking()
                .Where(l => l.LeaseId == leaseId)
                .Select(l => new
                {
                    l.Id, l.ChargeDefinitionId, l.ChargeDefinition.Name, l.ChargeDefinition.Type,
                    l.ChargeDefinition.DefaultAmount, l.ChargeDefinition.UnitRate, l.ChargeDefinition.UnitOfMeasure,
                    l.ChargeDefinition.TaxPercent, l.AmountOverride, l.UnitRateOverride
                })
                .ToListAsync(ct))
            .OrderBy(l => l.Type).ThenBy(l => l.Name)
            .Select(l => new LeaseChargeSummary(l.Id, l.ChargeDefinitionId, l.Name, l.Type,
                l.AmountOverride ?? l.DefaultAmount, l.UnitRateOverride ?? l.UnitRate, l.UnitOfMeasure, l.TaxPercent,
                l.AmountOverride is not null || l.UnitRateOverride is not null))
            .ToList();

    public async Task<Result<int>> AddLeaseChargeAsync(int leaseId, LeaseChargeInput input, CancellationToken ct = default)
    {
        var lease = await db.Leases.AsNoTracking().Where(l => l.Id == leaseId).Select(l => new { l.Status }).FirstOrDefaultAsync(ct);
        if (lease is null) return Result<int>.Fail("Lease not found.");
        if (lease.Status is not (LeaseStatus.Draft or LeaseStatus.Active)) return Result<int>.Fail("Charges can only be added to draft or active leases.");

        var def = await db.ChargeDefinitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == input.ChargeDefinitionId && c.IsActive, ct);
        if (def is null) return Result<int>.Fail("Choose a charge.");
        if (def.Type is ChargeType.OneTime or ChargeType.Penalty)
            return Result<int>.Fail("One-time charges and penalties go on an invoice, not on the lease.");
        if (input.AmountOverride < 0 || input.UnitRateOverride < 0) return Result<int>.Fail("Amounts can't be negative.");
        if (await db.LeaseCharges.AnyAsync(l => l.LeaseId == leaseId && l.ChargeDefinitionId == def.Id, ct))
            return Result<int>.Fail($"{def.Name} is already on this lease.");

        var charge = new LeaseCharge
        {
            LeaseId = leaseId,
            ChargeDefinitionId = def.Id,
            AmountOverride = def.Type == ChargeType.Recurring ? input.AmountOverride : null,
            UnitRateOverride = def.Type == ChargeType.Utility ? input.UnitRateOverride : null
        };
        db.LeaseCharges.Add(charge);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(charge.Id);
    }

    public async Task<Result> RemoveLeaseChargeAsync(int leaseChargeId, CancellationToken ct = default)
    {
        var charge = await db.LeaseCharges.FirstOrDefaultAsync(l => l.Id == leaseChargeId, ct);
        if (charge is null) return Result.Fail("Charge not found.");
        db.LeaseCharges.Remove(charge);   // soft delete; past invoices keep their lines
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ meter readings

    public async Task<IReadOnlyList<MeterSheetRow>> GetMeterSheetAsync(int assetId, int chargeDefinitionId, DateOnly readingDate, CancellationToken ct = default)
    {
        var def = await db.ChargeDefinitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == chargeDefinitionId && c.Type == ChargeType.Utility, ct);
        if (def is null) return [];

        var units = await db.Units.AsNoTracking()
            .Where(u => u.AssetId == assetId && u.Status != UnitStatus.Sold)
            .Select(u => new { u.Id, u.UnitNumber, NodeName = u.StructureNode != null ? u.StructureNode.Name : null })
            .ToListAsync(ct);
        var unitIds = units.Select(u => u.Id).ToList();

        var readings = await db.UtilityMeterReadings.AsNoTracking()
            .Where(r => unitIds.Contains(r.UnitId) && r.ChargeDefinitionId == chargeDefinitionId)
            .Select(r => new { r.UnitId, r.ReadingDate, r.CurrentReading })
            .ToListAsync(ct);

        var tenants = await db.Leases.AsNoTracking()
            .Where(l => unitIds.Contains(l.UnitId) && l.Status == LeaseStatus.Active)
            .Select(l => new { l.UnitId, l.Tenant.FullName, l.Id })
            .ToDictionaryAsync(l => l.UnitId, ct);
        var rates = await RatesForUnitsAsync(unitIds, def, ct);

        return units
            .OrderBy(u => u.NodeName ?? "", NaturalComparer.Instance).ThenBy(u => u.UnitNumber, NaturalComparer.Instance)
            .Select(u =>
            {
                var mine = readings.Where(r => r.UnitId == u.Id).ToList();
                var today = mine.FirstOrDefault(r => r.ReadingDate == readingDate);
                var previous = mine.Where(r => r.ReadingDate < readingDate).MaxBy(r => r.ReadingDate);
                return new MeterSheetRow(u.Id, u.UnitNumber, u.NodeName,
                    tenants.TryGetValue(u.Id, out var t) ? t.FullName : null,
                    previous?.CurrentReading, previous?.ReadingDate, rates[u.Id], today?.CurrentReading, today is not null);
            })
            .ToList();
    }

    public async Task<Result<int>> SaveReadingsAsync(int chargeDefinitionId, DateOnly readingDate, IReadOnlyList<MeterReadingEntry> entries, CancellationToken ct = default)
    {
        if (readingDate > clock.TodayEat()) return Result<int>.Fail("Readings can't be dated in the future.");

        var def = await db.ChargeDefinitions.AsNoTracking().FirstOrDefaultAsync(c => c.Id == chargeDefinitionId && c.Type == ChargeType.Utility, ct);
        if (def is null) return Result<int>.Fail("Choose a utility charge.");

        var toSave = entries.Where(e => e.Reading is not null).ToList();
        if (toSave.Count == 0) return Result<int>.Fail("Enter at least one reading.");

        var unitIds = toSave.Select(e => e.UnitId).Distinct().ToList();
        var units = await db.Units.AsNoTracking().Where(u => unitIds.Contains(u.Id)).Select(u => new { u.Id, u.UnitNumber }).ToDictionaryAsync(u => u.Id, ct);
        if (units.Count != unitIds.Count) return Result<int>.Fail("One of the units wasn't found.");

        var existing = await db.UtilityMeterReadings.AsNoTracking()
            .Where(r => unitIds.Contains(r.UnitId) && r.ChargeDefinitionId == chargeDefinitionId)
            .Select(r => new { r.UnitId, r.ReadingDate, r.CurrentReading })
            .ToListAsync(ct);
        var rates = await RatesForUnitsAsync(unitIds, def, ct);

        var errors = new List<string>();
        foreach (var entry in toSave)
        {
            var unitNo = units[entry.UnitId].UnitNumber;
            var mine = existing.Where(r => r.UnitId == entry.UnitId).ToList();
            if (mine.Any(r => r.ReadingDate == readingDate)) { errors.Add($"{unitNo}: already has a reading on this date."); continue; }
            if (mine.Any(r => r.ReadingDate > readingDate)) { errors.Add($"{unitNo}: has a later reading; readings must be entered in date order."); continue; }

            var previous = mine.MaxBy(r => r.ReadingDate)?.CurrentReading;
            var isOpening = entry.IsOpening || previous is null;
            var current = entry.Reading!.Value;

            var error = BillingRules.ReadingError(isOpening ? null : previous, current);
            if (error is not null) { errors.Add($"{unitNo}: {error}"); continue; }

            var consumption = isOpening ? 0m : current - previous!.Value;
            var rate = rates[entry.UnitId];
            db.UtilityMeterReadings.Add(new UtilityMeterReading
            {
                UnitId = entry.UnitId,
                ChargeDefinitionId = def.Id,
                ReadingDate = readingDate,
                PreviousReading = isOpening ? current : previous!.Value,
                CurrentReading = current,
                Consumption = consumption,
                UnitRate = rate,
                Amount = BillingRules.LineMath(consumption, rate, 0m).Amount,
                IsOpening = isOpening
            });
        }

        if (errors.Count > 0)
        {
            db.ChangeTracker.Clear();   // all or nothing
            return Result<int>.Fail(string.Join(" ", errors.Take(5)) + (errors.Count > 5 ? $" (+{errors.Count - 5} more)" : ""));
        }

        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(toSave.Count);
    }

    public async Task<IReadOnlyList<MeterReadingSummary>> ListReadingsAsync(bool unbilledOnly, int? assetId, CancellationToken ct = default)
    {
        var query = db.UtilityMeterReadings.AsNoTracking();
        if (unbilledOnly) query = query.Where(r => r.BilledInvoiceId == null && !r.IsOpening);
        if (assetId is int a) query = query.Where(r => r.Unit.AssetId == a);

        return await query
            .OrderByDescending(r => r.ReadingDate).ThenBy(r => r.Unit.UnitNumber)
            .Take(500)
            .Select(r => new MeterReadingSummary(r.Id, r.UnitId, r.Unit.UnitNumber, r.Unit.AssetId, r.Unit.Asset.Name,
                r.ChargeDefinition.Name, r.ChargeDefinition.UnitOfMeasure, r.ReadingDate, r.PreviousReading, r.CurrentReading,
                r.Consumption, r.UnitRate, r.Amount, r.IsOpening, r.BilledInvoiceId,
                r.BilledInvoice != null ? r.BilledInvoice.InvoiceNumber : null))
            .ToListAsync(ct);
    }

    public async Task<Result> DeleteReadingAsync(int readingId, CancellationToken ct = default)
    {
        var reading = await db.UtilityMeterReadings.FirstOrDefaultAsync(r => r.Id == readingId, ct);
        if (reading is null) return Result.Fail("Reading not found.");
        if (reading.BilledInvoiceId is not null) return Result.Fail("This reading has been billed. Cancel the invoice first.");
        if (await db.UtilityMeterReadings.AnyAsync(r => r.UnitId == reading.UnitId && r.ChargeDefinitionId == reading.ChargeDefinitionId && r.ReadingDate > reading.ReadingDate, ct))
            return Result.Fail("Only the latest reading on a meter can be deleted.");

        db.UtilityMeterReadings.Remove(reading);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Rate per unit: the active lease's override for this charge, else the charge's default.</summary>
    private async Task<Dictionary<int, decimal>> RatesForUnitsAsync(IReadOnlyCollection<int> unitIds, ChargeDefinition def, CancellationToken ct)
    {
        var overrides = await db.LeaseCharges.AsNoTracking()
            .Where(c => c.ChargeDefinitionId == def.Id && c.UnitRateOverride != null
                        && c.Lease.Status == LeaseStatus.Active && unitIds.Contains(c.Lease.UnitId))
            .Select(c => new { c.Lease.UnitId, Rate = c.UnitRateOverride!.Value })
            .ToListAsync(ct);
        var map = overrides.GroupBy(o => o.UnitId).ToDictionary(g => g.Key, g => g.First().Rate);
        return unitIds.ToDictionary(id => id, id => map.GetValueOrDefault(id, def.UnitRate ?? 0m));
    }

    private async Task<string?> ValidateDefinitionAsync(ChargeDefinitionInput input, int? id, CancellationToken ct)
    {
        var name = input.Name.Trim();
        if (name.Length == 0) return "Enter a name.";
        if (await db.ChargeDefinitions.AnyAsync(c => c.Name == name && c.Id != (id ?? 0), ct)) return $"You already have a charge called {name}.";
        if (input.TaxPercent is < 0 or > 100) return "Tax must be between 0 and 100%.";

        return input.Type switch
        {
            ChargeType.Utility when input.UnitRate is not > 0 => "Enter the price per unit (e.g. per m³ or kWh).",
            ChargeType.Utility when string.IsNullOrWhiteSpace(input.UnitOfMeasure) => "Enter the unit of measure, e.g. m³ or kWh.",
            ChargeType.Recurring when input.DefaultAmount is not > 0 => "Enter the amount charged each time it's billed.",
            ChargeType.Recurring when input.AnchorMonth is < 1 or > 12 => "Choose the month it's billed in.",
            _ when input.DefaultAmount < 0 => "Amounts can't be negative.",
            _ => null
        };
    }

    private static void Apply(ChargeDefinition def, ChargeDefinitionInput input)
    {
        def.Name = input.Name.Trim();
        def.Type = input.Type;
        def.DefaultAmount = input.Type == ChargeType.Utility ? null : input.DefaultAmount;
        def.UnitRate = input.Type == ChargeType.Utility ? input.UnitRate : null;
        def.UnitOfMeasure = input.Type == ChargeType.Utility ? input.UnitOfMeasure?.Trim() : null;
        def.TaxPercent = input.TaxPercent;
        def.Description = input.Description?.Trim();
        def.IsActive = input.IsActive;
        def.Frequency = input.Type == ChargeType.Recurring ? input.Frequency : ChargeFrequency.Monthly;
        def.AnchorMonth = input.Type == ChargeType.Recurring && input.Frequency != ChargeFrequency.Monthly ? Math.Clamp(input.AnchorMonth, 1, 12) : 1;
    }
}
