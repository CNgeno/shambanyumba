using System.ComponentModel.DataAnnotations;

namespace RealEstateERP.Core.Enums;

public enum ListingType
{
    [Display(Name = "To let")] Rent,
    [Display(Name = "For sale")] Sale
}

public enum ListingStatus
{
    Draft,
    Published,
    Paused,
    Closed
}

/// <summary>Billing state of a lead. Only Unlocked leads show contact details.</summary>
public enum LeadStatus
{
    /// <summary>Waiting for the enquirer to confirm their email. Never shown, never charged.</summary>
    Unverified,
    /// <summary>Confirmed, but the business had too few credits. Shown as "top up to view".</summary>
    [Display(Name = "Waiting for credits")] AwaitingCredits,
    Unlocked,
    Refunded
}

/// <summary>Where the business is with a lead.</summary>
public enum LeadStage
{
    New,
    Contacted,
    Viewing,
    Converted,
    Lost
}
