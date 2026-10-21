using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

public interface IBusinessService
{
    Task<IReadOnlyList<UserBusinessSummary>> GetBusinessesForUserAsync(string userId, CancellationToken ct = default);
    Task<UserBusinessSummary?> GetMembershipAsync(string userId, int businessId, CancellationToken ct = default);
    Task<int> CreateBusinessAsync(string ownerUserId, CreateBusinessRequest request, CancellationToken ct = default);
    Task<BusinessProfile?> GetProfileAsync(int businessId, CancellationToken ct = default);
    Task<Common.Result> UpdateProfileAsync(int businessId, BusinessProfileInput input, CancellationToken ct = default);
    Task<Common.Result> SetLogoAsync(int businessId, UploadedFile? logo, CancellationToken ct = default);
}
