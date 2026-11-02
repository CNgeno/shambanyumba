using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

/// <summary>Short-stay cancellation policies (booking.shambanyumba.com).</summary>
public enum CancellationPolicy
{
    [Display(Name = "Flexible: full deposit back up to 24 hours before")] Flexible,
    [Display(Name = "Moderate: full deposit back up to 5 days before, then 50%")] Moderate,
    [Display(Name = "Strict: 50% back up to 14 days before, then none")] Strict
}

public enum BookingStatus
{
    /// <summary>Booked on the guest site: dates held while the guest pays the deposit.</summary>
    [Display(Name = "Awaiting deposit")] Held,
    Confirmed,
    [Display(Name = "Checked in")] CheckedIn,
    [Display(Name = "Checked out")] CheckedOut,
    Cancelled,
    [Display(Name = "No-show")] NoShow
}

public enum BookingChannel
{
    [Display(Name = "Walk-in")] WalkIn,
    Phone,
    Direct,
    Airbnb,
    [Display(Name = "Booking.com")] BookingCom
}

public enum CleaningStatus { Pending, Done }

public enum StayPaymentMethod { [Display(Name = "M-PESA")] Mpesa, Cash, Bank }
