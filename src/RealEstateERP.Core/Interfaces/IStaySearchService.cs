using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// The guest site (booking.shambanyumba.com): published stays of every host. Works without a signed-in business:
/// reads filter by "published and not deleted" explicitly, and new rows carry the stay's own business.
/// Depends on: DbContext, TimeProvider, IEmailService.
/// </summary>
public interface IStaySearchService
{
    Task<IReadOnlyList<PublicStayCard>> SearchAsync(string? where, DateOnly? checkIn, DateOnly? checkOut, int guests, CancellationToken ct = default);
    Task<PublicStayDetails?> GetAsync(int stayUnitId, DateOnly? checkIn, DateOnly? checkOut, CancellationToken ct = default);
    /// <summary>Books as "awaiting deposit", holds the nights for BookingRules.DepositHold and emails the guest how to pay. Value: the booking number.</summary>
    Task<Result<string>> BookAsync(PublicBookingInput input, CancellationToken ct = default);
    /// <summary>The guest's view, found by booking number and the phone number used to book.</summary>
    Task<GuestBookingView?> FindAsync(string bookingNumber, string phone, CancellationToken ct = default);
    /// <summary>A published stay's photo (estate or unit), or null if it isn't one.</summary>
    Task<(Stream Content, string ContentType)?> OpenPhotoAsync(string kind, int photoId, CancellationToken ct = default);
    /// <summary>Releases unpaid holds that have expired. Returns how many.</summary>
    Task<int> ExpireHoldsAsync(CancellationToken ct = default);
}
