using RealEstateERP.Core.Common;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Entities;

/// <summary>
/// A unit offered as a short stay (booking.shambanyumba.com), with its listing details, rules and rates.
/// One per unit; stored in the "booking" schema. The unit itself (property, number, size) stays where it is.
/// </summary>
public class StayUnit : TenantEntity
{
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    // Listing
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MaxGuests { get; set; } = 2;
    public int Beds { get; set; } = 1;
    public int Bathrooms { get; set; } = 1;
    /// <summary>Comma-separated, e.g. "Wi-Fi, Parking, Hot shower".</summary>
    public string? Amenities { get; set; }
    public string? HouseRules { get; set; }
    public TimeOnly CheckInFrom { get; set; } = new(14, 0);
    public TimeOnly CheckOutBy { get; set; } = new(10, 0);

    // Rules
    public int MinNights { get; set; } = 1;
    public int MaxAdvanceDays { get; set; } = 365;
    public CancellationPolicy CancellationPolicy { get; set; } = CancellationPolicy.Moderate;
    /// <summary>Deposit due at booking, % of the total (30, 50 or 100).</summary>
    public int DepositPercent { get; set; } = 50;
    public bool IsPublished { get; set; }

    // Rates (KES)
    public decimal BaseNightly { get; set; }
    /// <summary>Friday and Saturday nights; null = base rate.</summary>
    public decimal? WeekendNightly { get; set; }
    public decimal CleaningFee { get; set; }
    /// <summary>Longest stay allowed; null = no limit.</summary>
    public int? MaxNights { get; set; }

    public ICollection<StaySeason> Seasons { get; set; } = new List<StaySeason>();
    /// <summary>Length-of-stay discounts, e.g. 5+ nights 5%, 14+ nights 12%, 27+ nights 20%: the longest that applies wins.</summary>
    public ICollection<StayDiscount> Discounts { get; set; } = new List<StayDiscount>();
}

/// <summary>A date range with its own nightly rate (e.g. "December holidays"). Both dates included.</summary>
public class StaySeason : TenantEntity
{
    public int StayUnitId { get; set; }
    public StayUnit StayUnit { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public decimal Nightly { get; set; }
}

/// <summary>A short-stay guest of this business.</summary>
public class Guest : TenantEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
}

/// <summary>A short-stay booking. The price is fixed when it is made (later rate changes don't affect it).</summary>
public class Booking : TenantEntity
{
    public string BookingNumber { get; set; } = string.Empty;
    public int StayUnitId { get; set; }
    public StayUnit StayUnit { get; set; } = null!;
    public int GuestId { get; set; }
    public Guest Guest { get; set; } = null!;
    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Nights { get; set; }
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public BookingChannel Channel { get; set; }
    public string? ExternalRef { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    /// <summary>Guest-site bookings start Held ("awaiting deposit") until this time; unpaid holds are released.</summary>
    public DateTime? HoldExpiresUtc { get; set; }
    public decimal NightsTotal { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal DepositDue { get; set; }
    public decimal AmountPaid { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public DateTime? CheckedOutAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public string? Notes { get; set; }

    public decimal Balance => Total - AmountPaid;

    public ICollection<BookingPayment> Payments { get; set; } = new List<BookingPayment>();
}

/// <summary>Money received for a booking, straight into the host's own M-PESA, bank or till (ShambaNyumba never holds it).</summary>
public class BookingPayment : TenantEntity
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public StayPaymentMethod Method { get; set; }
    public decimal Amount { get; set; }
    public DateOnly ReceivedOn { get; set; }
    /// <summary>M-PESA code or bank reference; an M-PESA code can be used only once.</summary>
    public string? Reference { get; set; }
    public string? ReceiptNumber { get; set; }
}

/// <summary>
/// One occupied night of a unit, by a booking or a block. A unique index on (UnitId, Night) for rows not deleted makes
/// double booking impossible, whatever the source and however close together two requests arrive.
/// </summary>
public class BookingNight : TenantEntity
{
    public int UnitId { get; set; }
    public DateOnly Night { get; set; }
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int? CalendarBlockId { get; set; }
    public CalendarBlock? CalendarBlock { get; set; }
}

/// <summary>Dates closed to bookings (owner use, maintenance). Both dates included.</summary>
public class CalendarBlock : TenantEntity
{
    public int StayUnitId { get; set; }
    public StayUnit StayUnit { get; set; } = null!;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Cleaning after a stay, created at check-out.</summary>
public class CleaningTask : TenantEntity
{
    public int StayUnitId { get; set; }
    public StayUnit StayUnit { get; set; } = null!;
    public int? BookingId { get; set; }
    public DateOnly Due { get; set; }
    public CleaningStatus Status { get; set; } = CleaningStatus.Pending;
    public DateTime? DoneAtUtc { get; set; }
    public string? DoneBy { get; set; }
}

/// <summary>A length-of-stay discount: stays of at least MinNights nights get Percent off the nights.</summary>
public class StayDiscount : TenantEntity
{
    public int StayUnitId { get; set; }
    public StayUnit StayUnit { get; set; } = null!;
    public int MinNights { get; set; }
    public int Percent { get; set; }
}
