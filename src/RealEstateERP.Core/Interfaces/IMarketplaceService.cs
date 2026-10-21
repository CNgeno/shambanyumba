using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// FR-12 business side: listings, photos and leads for the current business.
/// Depends on: DbContext, IFileStorage, ISystemConfigService, ICurrentUserService, TimeProvider.
/// Unlocking and refunding move credits through CreditLedger in the same save as the lead.
/// </summary>
public interface IListingService
{
    Task<IReadOnlyList<ListingSummary>> ListAsync(ListingStatus? status, CancellationToken ct = default);
    Task<ListingDetails?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<ListableUnit>> GetListableUnitsAsync(CancellationToken ct = default);
    Task<Result<int>> CreateAsync(ListingInput input, CancellationToken ct = default);
    Task<Result> UpdateAsync(int id, ListingInput input, CancellationToken ct = default);
    Task<Result> AddPhotosAsync(int id, IReadOnlyList<UploadedFile> photos, CancellationToken ct = default);
    Task<Result> RemovePhotoAsync(int id, int photoId, CancellationToken ct = default);
    Task<Result> MakeCoverAsync(int id, int photoId, CancellationToken ct = default);
    Task<Result> SetStatusAsync(int id, ListingStatus status, CancellationToken ct = default);
    Task<ListingPhoto?> GetPhotoAsync(int photoId, CancellationToken ct = default);

    Task<LeadsOverview> GetLeadsAsync(LeadStage? stage, CancellationToken ct = default);
    Task<Result> UnlockAsync(int leadId, CancellationToken ct = default);
    Task<Result> UpdateLeadAsync(int leadId, LeadStage stage, string? notes, CancellationToken ct = default);
    Task<Result> RefundAsync(int leadId, string reason, CancellationToken ct = default);
    Task<int> CountNewLeadsAsync(CancellationToken ct = default);

    /// <summary>Enquiries waiting for the person to confirm their email (still within the window), optionally for one property.</summary>
    Task<IReadOnlyList<PendingEnquiry>> GetAwaitingConfirmationAsync(int? assetId, CancellationToken ct = default);
    Task<PropertyEnquiries> GetPropertyEnquiriesAsync(int assetId, CancellationToken ct = default);

    /// <summary>
    /// Lists every vacant rental unit that has no open rental listing: title, estate description, rent, and photos
    /// (the unit's interiors first, then the estate's). Published straight away. Returns how many were listed.
    /// </summary>
    Task<int> AutoListVacantAsync(CancellationToken ct = default);
}

/// <summary>
/// FR-12 public side: search across all businesses' live listings, enquiries and verification. Anonymous.
/// Depends on: DbContext, IEmailService, ISystemConfigService, TimeProvider. Reads with the tenant filter off;
/// writes only platform-level rows (leads, credit ledger) plus a direct view-count update.
/// </summary>
public interface IMarketplaceService
{
    Task<PublicSearchResult> SearchAsync(PublicSearch search, CancellationToken ct = default);
    Task<PublicListingDetails?> GetAsync(int listingId, bool countView, CancellationToken ct = default);
    Task<(string Key, string FileName)?> GetPublicPhotoAsync(int photoId, CancellationToken ct = default);
    Task<IReadOnlyList<PublicListingCard>> LatestAsync(int count, CancellationToken ct = default);

    /// <param name="verifyUrlFormat">Absolute URL with {0} for the token.</param>
    /// <summary>Value: true when delivered to the owner straight away (email confirmed recently), false when a confirmation email was sent.</summary>
    Task<Result<bool>> SubmitEnquiryAsync(EnquiryInput input, string verifyUrlFormat, string leadsUrl, CancellationToken ct = default);
    Task<VerifyResult> VerifyAsync(string token, string leadsUrl, CancellationToken ct = default);
}
