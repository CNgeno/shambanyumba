using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;

namespace RealEstateERP.Core.Models;

public sealed record ListingInput(int UnitId, ListingType Type, string Title, string Description, decimal Price);

public sealed record ListingSummary(
    int Id, ListingType Type, string Title, decimal Price, ListingStatus Status, int UnitId, string UnitNumber, UnitStatus UnitStatus,
    int AssetId, string AssetName, int Photos, int ViewCount, int Leads, DateTime? PublishedAtUtc)
{
    public bool IsLive => Status == ListingStatus.Published && UnitStatus == UnitStatus.Available;
}

public sealed record ListingDetails(Listing Listing, IReadOnlyList<ListingPhoto> Photos, int Leads);

public sealed record ListableUnit(int Id, string UnitNumber, string AssetName, UnitType Type, decimal MonthlyRent, decimal? SalePrice, string SuggestedRentTitle, string SuggestedSaleTitle);

public sealed record PublicSearch(ListingType? Type, string? County, string? Text, decimal? MinPrice, decimal? MaxPrice, int? Bedrooms, UnitType? UnitType, int Page = 1);

public sealed record PublicListingCard(
    int Id, string Slug, ListingType Type, string Title, decimal Price, string Location, string? County, UnitType UnitType,
    int? Bedrooms, int? Bathrooms, decimal? SizeSqm, int? CoverPhotoId, string BusinessName, DateTime? PublishedAtUtc);

public sealed record PublicSearchResult(IReadOnlyList<PublicListingCard> Items, int Total, int Page, int PageSize, IReadOnlyList<string> Counties)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public sealed record PublicListingDetails(
    PublicListingCard Card, string Description, IReadOnlyList<int> PhotoIds, string AssetName, IReadOnlyList<PublicListingCard> MoreInProject);

/// <param name="EmailConfirmedRecently">This browser confirmed this email within the confirmation window (protected cookie): deliver without a new email.</param>
public sealed record EnquiryInput(int ListingId, string Name, string Phone, string Email, string? Message, bool Consent, bool EmailConfirmedRecently = false);

public sealed record VerifyResult(bool Verified, string? ListingTitle, string? BusinessName, string Message, string? Email = null);

public sealed record LeadRow(
    int Id, int ListingId, string ListingTitle, string? UnitNumber, DateTime CreatedAtUtc, LeadStatus Status, LeadStage Stage, bool IsRepeat,
    int CreditsCharged, DateTime? UnlockedAtUtc, string? Name, string? Phone, string? Email, string? Message, string? Notes)
{
    public bool ContactVisible => Status is LeadStatus.Unlocked or LeadStatus.Refunded;
}

public sealed record LeadsOverview(IReadOnlyList<LeadRow> Leads, int AwaitingCredits, int CostPerLead, int CreditBalance, int RefundDays);

/// <summary>An enquiry the person hasn't confirmed yet: no contact details (they're only released once confirmed).</summary>
public sealed record PendingEnquiry(int ListingId, string ListingTitle, string FirstName, DateTime ReceivedAtUtc, DateTime ConfirmBeforeUtc);

/// <summary>A property's marketing at a glance.</summary>
public sealed record PropertyEnquiries(int LiveListings, int NewLeads, int TotalLeads, int AwaitingConfirmation);
