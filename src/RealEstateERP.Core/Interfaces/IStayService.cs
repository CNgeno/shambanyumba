using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Short stays for the current business: which units, their details, rates, seasons and prices. Depends on: DbContext, TimeProvider.</summary>
public interface IStayService
{
    Task<IReadOnlyList<StayListItem>> ListAsync(CancellationToken ct = default);
    /// <summary>The stay with its unit, property and seasons, or null.</summary>
    Task<StayUnit?> GetAsync(int stayUnitId, CancellationToken ct = default);
    /// <summary>Turns a unit into a short stay (unpublished, with defaults from the unit). Not for a unit let on a lease.</summary>
    Task<Result<int>> EnableAsync(int unitId, CancellationToken ct = default);
    Task<Result> SaveAsync(int stayUnitId, StayDetailsInput input, CancellationToken ct = default);
    Task<Result> AddSeasonAsync(int stayUnitId, StaySeasonInput input, CancellationToken ct = default);
    Task<Result> RemoveSeasonAsync(int stayUnitId, int seasonId, CancellationToken ct = default);
    Task<StayQuote> QuoteAsync(int stayUnitId, DateOnly checkIn, DateOnly checkOut, CancellationToken ct = default);

    Task<Result> AddDiscountAsync(int stayUnitId, int minNights, int percent, CancellationToken ct = default);
    Task<Result> RemoveDiscountAsync(int stayUnitId, int discountId, CancellationToken ct = default);

    /// <summary>Is this property a short-stay property (purpose "Short stays")?</summary>
    Task<bool> IsShortStayPropertyAsync(int assetId, CancellationToken ct = default);
    /// <summary>
    /// Every unit of a short-stay property is a short stay: creates the missing ones. On these properties the unit form's
    /// rate is the nightly rate, so it moves to the stay and the unit's monthly rent stays 0.
    /// </summary>
    Task EnsureStaysAsync(int assetId, CancellationToken ct = default);
    Task<StayPropertySummary> GetPropertySummaryAsync(int assetId, CancellationToken ct = default);
}
