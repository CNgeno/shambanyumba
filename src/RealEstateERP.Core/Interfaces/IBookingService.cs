using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Short-stay bookings for the current business: calendar, bookings, blocks, check-in/out, cleaning. Depends on: DbContext, TimeProvider.</summary>
public interface IBookingService
{
    Task<HostCalendar> CalendarAsync(DateOnly from, int days, CancellationToken ct = default);
    Task<HostToday> TodayAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BookingListItem>> ListAsync(string view, CancellationToken ct = default);
    Task<Booking?> GetAsync(int bookingId, CancellationToken ct = default);

    /// <summary>A host-made booking (walk-in, phone, or entered from a channel): confirmed, priced from the stay's rates.</summary>
    Task<Result<int>> CreateAsync(NewBookingInput input, CancellationToken ct = default);
    Task<Result> CheckInAsync(int bookingId, CancellationToken ct = default);
    /// <summary>Checks the guest out and creates the cleaning task.</summary>
    Task<Result> CheckOutAsync(int bookingId, CancellationToken ct = default);
    /// <summary>Cancels (or marks a no-show) and frees the nights.</summary>
    Task<Result> CancelAsync(int bookingId, string reason, bool noShow, CancellationToken ct = default);

    Task<Result<int>> BlockAsync(int stayUnitId, DateOnly from, DateOnly to, string reason, CancellationToken ct = default);
    Task<Result> UnblockAsync(int blockId, CancellationToken ct = default);

    /// <summary>Records money received; the balance goes down and the guest gets a receipt by email (if they have one).</summary>
    Task<Result<int>> RecordPaymentAsync(int bookingId, StayPaymentMethod method, decimal amount, DateOnly receivedOn, string? reference, CancellationToken ct = default);
    /// <summary>Emails the guest the booking details and how to pay (the business's payment instructions, booking number as account).</summary>
    Task<Result> SendConfirmationAsync(int bookingId, CancellationToken ct = default);

    Task<IReadOnlyList<CleaningItem>> CleaningAsync(bool includeDone, CancellationToken ct = default);
    Task<Result> CompleteCleaningAsync(int taskId, CancellationToken ct = default);
}
