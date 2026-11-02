using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

/// <summary>A unit on the host's Stays list: already a short stay or not yet.</summary>
public sealed record StayListItem(int UnitId, string UnitNumber, string PropertyName, UnitType Type, int? Bedrooms,
    bool IsLet, int? StayUnitId, string? Title, bool IsPublished, decimal? BaseNightly);

public sealed record StayDetailsInput(
    string Title, string? Description, int MaxGuests, int Beds, int Bathrooms, string? Amenities, string? HouseRules,
    TimeOnly CheckInFrom, TimeOnly CheckOutBy, int MinNights, int MaxAdvanceDays, CancellationPolicy CancellationPolicy,
    int DepositPercent, decimal BaseNightly, decimal? WeekendNightly, decimal CleaningFee, int? MaxNights, bool IsPublished);

public sealed record StaySeasonInput(string Name, DateOnly From, DateOnly To, decimal Nightly);

public sealed record NewBookingInput(int StayUnitId, DateOnly CheckIn, DateOnly CheckOut, int Adults, int Children,
    string GuestName, string GuestPhone, string? GuestEmail, BookingChannel Channel, string? ExternalRef, string? Notes);

/// <summary>One cell of the host calendar: free, booked (with the booking) or blocked.</summary>
public sealed record CalendarCell(DateOnly Night, int? BookingId, string? BookingNumber, string? GuestName, BookingStatus? Status,
    BookingChannel? Channel, bool IsFirstNight, int? BlockId, string? BlockReason);

public sealed record CalendarRow(int StayUnitId, string Title, string UnitNumber, IReadOnlyList<CalendarCell> Cells);

public sealed record HostCalendar(DateOnly From, int Days, IReadOnlyList<CalendarRow> Rows);

public sealed record BookingListItem(int Id, string BookingNumber, string StayTitle, string GuestName, string GuestPhone,
    DateOnly CheckIn, DateOnly CheckOut, int Nights, BookingStatus Status, BookingChannel Channel, decimal Total, decimal AmountPaid);

public sealed record CleaningItem(int Id, string StayTitle, string UnitNumber, DateOnly Due, CleaningStatus Status, string? BookingNumber);

/// <summary>The host's day: who arrives, who leaves, who is in, what to clean.</summary>
public sealed record HostToday(DateOnly Date, IReadOnlyList<BookingListItem> Arrivals, IReadOnlyList<BookingListItem> Departures,
    IReadOnlyList<BookingListItem> InHouse, IReadOnlyList<CleaningItem> Cleaning, IReadOnlyList<BookingListItem>? AwaitingDeposit = null);

/// <summary>A short-stay property at a glance (main site property page).</summary>
public sealed record StayPropertySummary(int Units, int Published, decimal? MinNightly, decimal? MaxNightly,
    int NightsBookedThisMonth, int NightsInMonth, int FreeTonight, decimal BookingIncomeThisMonth, IReadOnlyList<StayPropertyUnit> Rows,
    decimal ExpensesThisMonth = 0)
{
    public decimal NetThisMonth => BookingIncomeThisMonth - ExpensesThisMonth;
    public int OccupancyPercent => NightsInMonth == 0 ? 0 : (int)Math.Round(100.0 * NightsBookedThisMonth / NightsInMonth);
}

public sealed record StayPropertyUnit(int UnitId, string UnitNumber, int StayUnitId, string Title, decimal Nightly, bool IsPublished, bool OccupiedTonight);

// ---------------------------------------------------------------- guest site (public)

/// <summary>A published stay in search results; Quote set when dates were given (null = no dates, or not bookable then).</summary>
public sealed record PublicStayCard(int StayUnitId, string Title, string Place, int Beds, int MaxGuests, decimal FromNightly,
    int? PhotoId, string? PhotoKind, StayQuote? Quote);

public sealed record PublicPhoto(int Id, string Kind, string? Caption);

public sealed record PublicStayDetails(PublicStayCard Card, string HostName, string? Description, IReadOnlyList<string> Amenities, string? HouseRules,
    TimeOnly CheckInFrom, TimeOnly CheckOutBy, int MinNights, int? MaxNights, CancellationPolicy CancellationPolicy, int DepositPercent,
    decimal CleaningFee, IReadOnlyList<PublicPhoto> Photos, IReadOnlyList<DateOnly> TakenNights, IReadOnlyList<string> Discounts);

public sealed record PublicBookingInput(int StayUnitId, DateOnly CheckIn, DateOnly CheckOut, int Adults, int Children,
    string Name, string Phone, string? Email, string? Message);

/// <summary>What a guest sees under "My booking".</summary>
public sealed record GuestBookingView(string BookingNumber, BookingStatus Status, string StayTitle, string Place, string HostName, string? HostPhone,
    DateOnly CheckIn, DateOnly CheckOut, int Nights, TimeOnly CheckInFrom, TimeOnly CheckOutBy, decimal Total, decimal AmountPaid, decimal DepositDue,
    DateTime? HoldExpiresUtc, string? PaymentInstructions)
{
    public decimal Balance => Total - AmountPaid;
    public decimal DepositOutstanding => Math.Max(0, DepositDue - AmountPaid);
}
