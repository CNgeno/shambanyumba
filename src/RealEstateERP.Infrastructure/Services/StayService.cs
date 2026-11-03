using Microsoft.EntityFrameworkCore;
using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

public sealed class StayService(ApplicationDbContext db, TimeProvider clock, ICurrentUserService? currentUser = null) : IStayService
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(3));   // East Africa Time

    /// <summary>The business's short stays: every unit of its short-stay properties (created if missing).</summary>
    public async Task<IReadOnlyList<StayListItem>> ListAsync(CancellationToken ct = default)
    {
        foreach (var assetId in await db.Assets.Where(a => a.Purpose == PropertyPurpose.ShortStay).Select(a => a.Id).ToListAsync(ct))
            await EnsureStaysAsync(assetId, ct);
        return await db.StayUnits.AsNoTracking().Where(s => s.Unit.Status != UnitStatus.Inactive)
            .OrderBy(s => s.Unit.Asset.Name).ThenBy(s => s.Unit.UnitNumber)
            .Select(s => new StayListItem(s.UnitId, s.Unit.UnitNumber, s.Unit.Asset.Name, s.Unit.Type, s.Unit.Bedrooms, false,
                s.Id, s.Title, s.IsPublished, s.BaseNightly))
            .ToListAsync(ct);
    }

    public Task<StayUnit?> GetAsync(int stayUnitId, CancellationToken ct = default)
        => db.StayUnits.Include(s => s.Unit).ThenInclude(u => u.Asset).Include(s => s.Seasons.OrderBy(x => x.From))
            .Include(s => s.Discounts.OrderBy(x => x.MinNights))
            .FirstOrDefaultAsync(s => s.Id == stayUnitId, ct);

    public async Task<Result<int>> EnableAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await db.Units.Include(u => u.Asset).FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return Result<int>.Fail("Unit not found.");
        if (await db.StayUnits.FirstOrDefaultAsync(s => s.UnitId == unitId, ct) is { } existing) return Result<int>.Ok(existing.Id);
        if (await db.Leases.AnyAsync(l => l.UnitId == unitId && l.Status == LeaseStatus.Active, ct))
            return Result<int>.Fail($"{unit.UnitNumber} is let on an active lease. End the lease first, or choose a vacant unit.");
        var bedrooms = unit.Bedrooms ?? 1;
        var stay = new StayUnit
        {
            UnitId = unit.Id,
            Title = $"{(bedrooms <= 0 ? "Studio" : $"{bedrooms}-bedroom")} at {unit.Asset.Name}",
            Beds = Math.Max(1, bedrooms), Bathrooms = unit.Bathrooms ?? 1, MaxGuests = Math.Max(2, bedrooms * 2),
            IsPublished = false
        };
        db.StayUnits.Add(stay);
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(stay.Id);
    }

    public async Task<Result> SaveAsync(int stayUnitId, StayDetailsInput i, CancellationToken ct = default)
    {
        var stay = await db.StayUnits.FirstOrDefaultAsync(s => s.Id == stayUnitId, ct);
        if (stay is null) return Result.Fail("Short stay not found.");
        if (Validate(i) is string error) return Result.Fail(error);
        stay.Title = i.Title.Trim();
        stay.Description = Clean(i.Description);
        stay.MaxGuests = i.MaxGuests; stay.Beds = i.Beds; stay.Bathrooms = i.Bathrooms;
        stay.Amenities = Clean(i.Amenities); stay.HouseRules = Clean(i.HouseRules);
        stay.CheckInFrom = i.CheckInFrom; stay.CheckOutBy = i.CheckOutBy;
        stay.MinNights = i.MinNights; stay.MaxAdvanceDays = i.MaxAdvanceDays;
        stay.CancellationPolicy = i.CancellationPolicy; stay.DepositPercent = i.DepositPercent;
        stay.BaseNightly = i.BaseNightly; stay.WeekendNightly = i.WeekendNightly is > 0 ? i.WeekendNightly : null;
        stay.CleaningFee = i.CleaningFee; stay.MaxNights = i.MaxNights is > 0 ? i.MaxNights : null;
        stay.IsPublished = i.IsPublished;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> AddSeasonAsync(int stayUnitId, StaySeasonInput i, CancellationToken ct = default)
    {
        if (!await db.StayUnits.AnyAsync(s => s.Id == stayUnitId, ct)) return Result.Fail("Short stay not found.");
        if (string.IsNullOrWhiteSpace(i.Name)) return Result.Fail("Give the season a name, e.g. \"December holidays\".");
        if (i.To < i.From) return Result.Fail("The season must end on or after it starts.");
        if (i.Nightly <= 0) return Result.Fail("Enter the season's nightly rate.");
        var clash = await db.StaySeasons.Where(s => s.StayUnitId == stayUnitId && s.From <= i.To && i.From <= s.To).Select(s => s.Name).FirstOrDefaultAsync(ct);
        if (clash is not null) return Result.Fail($"Those dates overlap the season \"{clash}\".");
        db.StaySeasons.Add(new StaySeason { StayUnitId = stayUnitId, Name = i.Name.Trim(), From = i.From, To = i.To, Nightly = i.Nightly });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveSeasonAsync(int stayUnitId, int seasonId, CancellationToken ct = default)
    {
        var season = await db.StaySeasons.FirstOrDefaultAsync(s => s.Id == seasonId && s.StayUnitId == stayUnitId, ct);
        if (season is null) return Result.Fail("Season not found.");
        season.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<StayQuote> QuoteAsync(int stayUnitId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default)
    {
        var stay = await db.StayUnits.AsNoTracking().Include(s => s.Seasons).Include(s => s.Discounts).FirstOrDefaultAsync(s => s.Id == stayUnitId, ct);
        return stay is null ? StayQuote.Invalid("Short stay not found.") : StayPricing.Quote(stay, stay.Seasons, checkIn, checkOut, Today);
    }

    public async Task<Result> AddDiscountAsync(int stayUnitId, int minNights, int percent, CancellationToken ct = default)
    {
        if (!await db.StayUnits.AnyAsync(s => s.Id == stayUnitId, ct)) return Result.Fail("Short stay not found.");
        if (minNights is < 2 or > 365) return Result.Fail("Discounts start from stays of 2 nights up to 365.");
        if (percent is < 1 or > 90) return Result.Fail("The discount must be between 1% and 90%.");
        if (await db.StayDiscounts.AnyAsync(d => d.StayUnitId == stayUnitId && d.MinNights == minNights, ct))
            return Result.Fail($"There's already a discount for {minNights}+ nights; remove it first to change it.");
        db.StayDiscounts.Add(new StayDiscount { StayUnitId = stayUnitId, MinNights = minNights, Percent = percent });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> RemoveDiscountAsync(int stayUnitId, int discountId, CancellationToken ct = default)
    {
        var d = await db.StayDiscounts.FirstOrDefaultAsync(x => x.Id == discountId && x.StayUnitId == stayUnitId, ct);
        if (d is null) return Result.Fail("Discount not found.");
        db.StayDiscounts.Remove(d);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetPublishedAsync(int stayUnitId, bool published, CancellationToken ct = default)
    {
        var stay = await db.StayUnits.FirstOrDefaultAsync(s => s.Id == stayUnitId, ct);
        if (stay is null) return Result.Fail("Short stay not found.");
        if (published && stay.BaseNightly <= 0) return Result.Fail("Set the nightly rate before publishing.");
        stay.IsPublished = published;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<int>> PublishAllReadyAsync(CancellationToken ct = default)
    {
        var drafts = await db.StayUnits.Where(s => !s.IsPublished && s.BaseNightly > 0 && s.Unit.Status != UnitStatus.Inactive).ToListAsync(ct);
        foreach (var s in drafts) s.IsPublished = true;
        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(drafts.Count);
    }

    public async Task<IReadOnlyList<HostStayProperty>> ListPropertiesAsync(CancellationToken ct = default)
        => await db.Assets.AsNoTracking().Where(a => a.Purpose == PropertyPurpose.ShortStay).OrderBy(a => a.Name)
            .Select(a => new HostStayProperty(a.Id, a.Name, a.Location, a.County,
                db.Units.Count(u => u.AssetId == a.Id && u.Status != UnitStatus.Inactive),
                db.StayUnits.Count(s => s.Unit.AssetId == a.Id && s.IsPublished),
                db.AssetPhotos.Count(p => p.AssetId == a.Id) + db.UnitPhotos.Count(p => p.Unit.AssetId == a.Id)))
            .ToListAsync(ct);

    public async Task<HostSetupProgress> GetSetupProgressAsync(CancellationToken ct = default)
    {
        var properties = await ListPropertiesAsync(ct);
        var stays = db.StayUnits.Where(s => s.Unit.Status != UnitStatus.Inactive);
        var businessId = currentUser?.BusinessId;
        var instructions = businessId is null ? null
            : await db.Businesses.AsNoTracking().Where(b => b.Id == businessId).Select(b => b.PaymentInstructions).FirstOrDefaultAsync(ct);
        return new HostSetupProgress(
            properties.Count > 0,
            properties.Any(p => p.Units > 0),
            properties.Any(p => p.Photos > 0),
            await stays.AnyAsync(ct) && !await stays.AnyAsync(s => s.BaseNightly <= 0, ct),
            await stays.AnyAsync(s => s.IsPublished, ct),
            !string.IsNullOrWhiteSpace(instructions));
    }

    public Task<bool> IsShortStayPropertyAsync(int assetId, CancellationToken ct = default)
        => db.Assets.AnyAsync(a => a.Id == assetId && a.Purpose == PropertyPurpose.ShortStay, ct);

    public async Task EnsureStaysAsync(int assetId, CancellationToken ct = default)
    {
        if (!await IsShortStayPropertyAsync(assetId, ct)) return;
        var asset = await db.Assets.FirstAsync(a => a.Id == assetId, ct);
        var units = await db.Units.Where(u => u.AssetId == assetId && u.Status != UnitStatus.Inactive).ToListAsync(ct);
        var stays = await db.StayUnits.Where(s => s.Unit.AssetId == assetId).ToDictionaryAsync(s => s.UnitId, ct);
        var changed = false;
        foreach (var u in units)
        {
            if (!stays.TryGetValue(u.Id, out var stay))
            {
                var bedrooms = u.Bedrooms ?? 1;
                stay = new StayUnit
                {
                    UnitId = u.Id, Title = $"{(bedrooms <= 0 ? "Studio" : $"{bedrooms}-bedroom")} {u.UnitNumber} at {asset.Name}",
                    Beds = Math.Max(1, bedrooms), Bathrooms = u.Bathrooms ?? 1, MaxGuests = Math.Max(2, bedrooms * 2)
                };
                db.StayUnits.Add(stay);
                changed = true;
            }
            // On a short-stay property the unit form's rate is the nightly rate: it belongs on the stay, never as rent.
            if (u.MonthlyRent > 0)
            {
                stay.BaseNightly = u.MonthlyRent;
                u.MonthlyRent = 0;
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }

    public async Task<StayPropertySummary> GetPropertySummaryAsync(int assetId, CancellationToken ct = default)
    {
        await EnsureStaysAsync(assetId, ct);
        var today = Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var stays = await db.StayUnits.AsNoTracking().Where(s => s.Unit.AssetId == assetId && s.Unit.Status != UnitStatus.Inactive)
            .OrderBy(s => s.Unit.UnitNumber)
            .Select(s => new { s.Id, s.UnitId, s.Unit.UnitNumber, s.Title, s.BaseNightly, s.IsPublished }).ToListAsync(ct);
        var unitIds = stays.Select(s => s.UnitId).ToList();
        var bookedNights = await db.BookingNights.AsNoTracking()
            .Where(n => unitIds.Contains(n.UnitId) && n.BookingId != null && n.Night >= monthStart && n.Night < monthEnd).CountAsync(ct);
        var tonight = await db.BookingNights.AsNoTracking().Where(n => unitIds.Contains(n.UnitId) && n.Night == today).Select(n => n.UnitId).ToListAsync(ct);
        var stayIds = stays.Select(s => s.Id).ToList();
        var income = await db.Bookings.AsNoTracking()
            .Where(b => stayIds.Contains(b.StayUnitId) && b.CheckIn >= monthStart && b.CheckIn < monthEnd
                        && b.Status != BookingStatus.Cancelled && b.Status != BookingStatus.Held)
            .SumAsync(b => (decimal?)b.Total, ct) ?? 0m;
        var expenses = await db.ProjectCosts.AsNoTracking()
            .Where(c => c.AssetId == assetId && c.Kind == CostKind.Actual && c.Category >= CostCategory.StayCleaning
                        && c.Date >= monthStart && c.Date < monthEnd)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        var rates = stays.Where(s => s.BaseNightly > 0).Select(s => s.BaseNightly).ToList();
        return new StayPropertySummary(stays.Count, stays.Count(s => s.IsPublished), rates.Count > 0 ? rates.Min() : null, rates.Count > 0 ? rates.Max() : null,
            bookedNights, stays.Count * (monthEnd.DayNumber - monthStart.DayNumber), stays.Count(s => !tonight.Contains(s.UnitId)), income,
            stays.Select(s => new StayPropertyUnit(s.UnitId, s.UnitNumber, s.Id, s.Title, s.BaseNightly, s.IsPublished, tonight.Contains(s.UnitId))).ToList(),
            expenses);
    }

    private static string? Validate(StayDetailsInput i)
        => string.IsNullOrWhiteSpace(i.Title) ? "Give the stay a title, e.g. \"2-bedroom at Bethlehem Estate\"."
        : i.MaxGuests is < 1 or > 50 ? "Guests must be between 1 and 50."
        : i.Beds < 1 ? "At least one bed."
        : i.MinNights is < 1 or > 365 ? "Minimum nights must be between 1 and 365."
        : i.MaxAdvanceDays is < 1 or > 730 ? "Bookings can open between 1 and 730 days ahead."
        : i.DepositPercent is < 0 or > 100 ? "Deposit must be between 0% and 100%."
        : i.BaseNightly < 0 || i.CleaningFee < 0 || i.WeekendNightly < 0 ? "Rates can't be negative."
        : i.MaxNights is < 1 or > 365 ? "The longest stay must be between 1 and 365 nights (or blank for no limit)."
        : i.MaxNights is int max && max < i.MinNights ? "The longest stay can't be shorter than the minimum stay."
        : i.IsPublished && i.BaseNightly <= 0 ? "Set the nightly rate before publishing."
        : null;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
