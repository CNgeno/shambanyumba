using System.ComponentModel.DataAnnotations;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Booking.Web.Models;

/// <summary>The Edit stay form: listing details, rules and rates.</summary>
public sealed class StayEditModel
{
    [Required, StringLength(150)] public string Title { get; set; } = "";
    [StringLength(4000)] public string? Description { get; set; }
    [Range(1, 50), Display(Name = "Maximum guests")] public int MaxGuests { get; set; } = 2;
    [Range(1, 50)] public int Beds { get; set; } = 1;
    [Range(0, 20)] public int Bathrooms { get; set; } = 1;
    [StringLength(1000), Display(Name = "Amenities (comma-separated)")] public string? Amenities { get; set; }
    [StringLength(2000), Display(Name = "House rules")] public string? HouseRules { get; set; }
    [Display(Name = "Check-in from")] public TimeOnly CheckInFrom { get; set; } = new(14, 0);
    [Display(Name = "Check-out by")] public TimeOnly CheckOutBy { get; set; } = new(10, 0);
    [Range(1, 365), Display(Name = "Minimum nights")] public int MinNights { get; set; } = 1;
    [Range(1, 730), Display(Name = "Book up to (days ahead)")] public int MaxAdvanceDays { get; set; } = 365;
    [Display(Name = "Cancellation policy")] public CancellationPolicy CancellationPolicy { get; set; }
    [Range(0, 100), Display(Name = "Deposit at booking (%)")] public int DepositPercent { get; set; } = 50;
    [Range(0, 10_000_000), Display(Name = "Nightly rate (KES)")] public decimal BaseNightly { get; set; }
    [Range(0, 10_000_000), Display(Name = "Weekend rate, Fri and Sat nights (blank = nightly rate)")] public decimal? WeekendNightly { get; set; }
    [Range(0, 1_000_000), Display(Name = "Cleaning fee per stay (KES)")] public decimal CleaningFee { get; set; }
    [Range(1, 365), Display(Name = "Longest stay in nights (blank = no limit)")] public int? MaxNights { get; set; }
    [Display(Name = "Published (guests can find and book it)")] public bool IsPublished { get; set; }

    public StayDetailsInput ToInput() => new(Title, Description, MaxGuests, Beds, Bathrooms, Amenities, HouseRules, CheckInFrom, CheckOutBy,
        MinNights, MaxAdvanceDays, CancellationPolicy, DepositPercent, BaseNightly, WeekendNightly, CleaningFee, MaxNights, IsPublished);
}
