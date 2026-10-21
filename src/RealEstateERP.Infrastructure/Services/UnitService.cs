using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

/// <summary>
/// Depends on: DbContext, ISubscriptionService (plan unit limit), ICurrentUserService.
/// Must never depend on AssetService, leases, invoicing or anything "above" it.
/// </summary>
public sealed class UnitService(
    ApplicationDbContext db,
    ISubscriptionService subscriptions,
    ICurrentUserService currentUser) : IUnitService
{
    public Task<Unit?> GetAsync(int unitId, CancellationToken ct = default)
        => db.Units.AsNoTracking().Include(u => u.Asset).FirstOrDefaultAsync(u => u.Id == unitId, ct);

    public async Task<UnitUsage> GetUsageAsync(CancellationToken ct = default)
    {
        var snapshot = await subscriptions.GetSnapshotAsync(currentUser.BusinessId ?? 0, ct);
        // Units in use: everything active, including sold units still being paid off; sold and completed units stop counting.
        var used = await db.Units.CountAsync(u => u.Status != UnitStatus.Inactive
            && (u.Status != UnitStatus.Sold || db.Sales.Any(sa => sa.UnitId == u.Id && sa.Status == SaleStatus.Active)), ct);
        return new UnitUsage(used, snapshot.UnitLimit, snapshot.PlanName);
    }

    public async Task<Result<int>> CreateAsync(UnitInput input, CancellationToken ct = default)
    {
        var error = await ValidateAsync(input.AssetId, input.StructureNodeId, input.UnitNumber, null, input, ct);
        if (error is not null) return Result<int>.Fail(error);

        var capacity = await CheckCapacityAsync(1, ct);
        if (!capacity.Succeeded) return Result<int>.Fail(capacity.Error!);

        var unit = new Unit { AssetId = input.AssetId, Status = UnitStatus.Available };
        Apply(unit, input);
        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(unit.Id);
    }

    public async Task<Result<int>> BulkCreateAsync(BulkUnitInput input, CancellationToken ct = default)
    {
        if (input.Count is < 1 or > BulkUnitInput.MaxCount)
            return Result<int>.Fail($"Create between 1 and {BulkUnitInput.MaxCount} units at a time.");
        if (input.StartNumber < 0)
            return Result<int>.Fail("The starting number can't be negative.");
        if (input.SalePrice is < 0) return Result<int>.Fail("The sale price can't be negative.");
        if (input.MonthlyRent < 0)
            return Result<int>.Fail("Rent can't be negative.");

        if (!await db.Assets.AnyAsync(a => a.Id == input.AssetId, ct))
            return Result<int>.Fail("Property not found.");
        if (input.StructureNodeId is int nodeId &&
            !await db.StructureNodes.AnyAsync(n => n.Id == nodeId && n.AssetId == input.AssetId, ct))
            return Result<int>.Fail("The chosen block or floor isn't part of this property.");

        var numbers = input.UnitNumbers();
        if (numbers.Any(n => n.Length > 30))
            return Result<int>.Fail("Unit numbers can be at most 30 characters.");

        var existing = await db.Units
            .Where(u => u.AssetId == input.AssetId && numbers.Contains(u.UnitNumber))
            .Select(u => u.UnitNumber)
            .ToListAsync(ct);
        if (existing.Count > 0)
            return Result<int>.Fail("These unit numbers already exist: " +
                string.Join(", ", existing.OrderBy(x => x, NaturalComparer.Instance).Take(5)) +
                (existing.Count > 5 ? $" and {existing.Count - 5} more." : "."));

        var capacity = await CheckCapacityAsync(numbers.Count, ct);
        if (!capacity.Succeeded) return Result<int>.Fail(capacity.Error!);

        foreach (var number in numbers)
        {
            db.Units.Add(new Unit
            {
                AssetId = input.AssetId,
                StructureNodeId = input.StructureNodeId,
                UnitNumber = number,
                FloorNumber = input.FloorNumber,
                Type = input.Type,
                SizeSqm = input.SizeSqm,
                Bedrooms = input.Bedrooms,
                Bathrooms = input.Bathrooms,
                MonthlyRent = input.MonthlyRent,
                SalePrice = input.SalePrice,
                Status = UnitStatus.Available
            });
        }
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(numbers.Count);
    }

    public async Task<Result> UpdateAsync(int unitId, UnitInput input, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return Result.Fail("Unit not found.");

        // A unit never moves between properties; AssetId from the form is ignored.
        var error = await ValidateAsync(unit.AssetId, input.StructureNodeId, input.UnitNumber, unitId, input, ct);
        if (error is not null) return Result.Fail(error);

        Apply(unit, input);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> ChangeStatusAsync(int unitId, UnitStatus status, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return Result.Fail("Unit not found.");

        var error = UnitStatusRules.ManualChangeError(unit.Status, status);
        if (error is not null) return Result.Fail(error);
        if (await db.Sales.AnyAsync(s => s.UnitId == unitId && s.Status != SaleStatus.Cancelled, ct))
            return Result.Fail("This unit has a sale. Its status follows the sale (cancel the sale to free it).");

        if (!UnitStatusRules.CountsTowardLimit(unit.Status) && UnitStatusRules.CountsTowardLimit(status))
        {
            var capacity = await CheckCapacityAsync(1, ct);
            if (!capacity.Succeeded) return capacity;
        }

        unit.Status = status;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail("This unit was changed by someone else. Refresh and try again.");
        }
        return Result.Ok();
    }

    public async Task<Result> DeleteAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return Result.Fail("Unit not found.");

        if (await db.Sales.AnyAsync(s => s.UnitId == unitId, ct))
            return Result.Fail($"Unit {unit.UnitNumber} has sale history, so it's kept. Mark it inactive instead.");
        if (await db.Leases.AnyAsync(l => l.UnitId == unitId, ct))
            return Result.Fail($"Unit {unit.UnitNumber} has lease history, so it's kept. Mark it inactive instead.");
        if (unit.Status is not (UnitStatus.Available or UnitStatus.Inactive))
            return Result.Fail($"Only available or inactive units can be deleted. {unit.UnitNumber} is {unit.Status.ToString().ToLowerInvariant()}.");

        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------------

    private async Task<Result> CheckCapacityAsync(int adding, CancellationToken ct)
    {
        var usage = await GetUsageAsync(ct);
        if (usage.Limit is not int limit || usage.Used + adding <= limit)
            return Result.Ok();

        var left = Math.Max(0, limit - usage.Used);
        return Result.Fail(left == 0
            ? $"Your {usage.PlanName} plan allows {limit} units and you're using all of them. Upgrade your plan to add more."
            : $"Your {usage.PlanName} plan allows {limit} units; you can add {left} more. Upgrade your plan to add {adding}.");
    }

    private async Task<string?> ValidateAsync(int assetId, int? nodeId, string unitNumber, int? unitId, UnitInput input, CancellationToken ct)
    {
        var number = unitNumber.Trim();
        if (number.Length == 0) return "Enter the unit number.";
        if (input.MonthlyRent < 0 || input.SalePrice < 0 || input.LatePenaltyAmount < 0)
            return "Amounts can't be negative.";

        if (!await db.Assets.AnyAsync(a => a.Id == assetId, ct))
            return "Property not found.";
        if (nodeId is int nid && !await db.StructureNodes.AnyAsync(n => n.Id == nid && n.AssetId == assetId, ct))
            return "The chosen block or floor isn't part of this property.";
        if (await db.Units.AnyAsync(u => u.AssetId == assetId && u.UnitNumber == number && u.Id != (unitId ?? 0), ct))
            return $"Unit {number} already exists in this property.";

        return null;
    }

    private static void Apply(Unit unit, UnitInput input)
    {
        unit.StructureNodeId = input.StructureNodeId;
        unit.UnitNumber = input.UnitNumber.Trim();
        unit.FloorNumber = input.FloorNumber;
        unit.Type = input.Type;
        unit.SizeSqm = input.SizeSqm;
        unit.Bedrooms = input.Bedrooms;
        unit.Bathrooms = input.Bathrooms;
        unit.MonthlyRent = input.MonthlyRent;
        unit.SalePrice = input.SalePrice;
        unit.GracePeriodDays = input.GracePeriodDays;
        unit.LatePenaltyAmount = input.LatePenaltyAmount;
        unit.Notes = input.Notes?.Trim();
    }
}
