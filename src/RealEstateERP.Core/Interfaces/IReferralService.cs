using RealEstateERP.Core.Common;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

/// <summary>Referral partners and codes (platform admin), and attaching a code to a new business. Depends on: DbContext, TimeProvider.</summary>
public interface IReferralService
{
    Task<IReadOnlyList<PartnerRow>> ListPartnersAsync(CancellationToken ct = default);
    Task<PartnerDetails?> GetPartnerAsync(int partnerId, CancellationToken ct = default);
    Task<Result<int>> CreatePartnerAsync(PartnerInput input, CancellationToken ct = default);
    Task<Result> UpdatePartnerAsync(int partnerId, PartnerInput input, CancellationToken ct = default);
    Task<Result> MarkCommissionPaidAsync(int commissionId, string reference, CancellationToken ct = default);

    /// <summary>Records which partner brought a new business. Unknown or inactive codes are ignored (returns false).</summary>
    Task<bool> AttachAsync(int businessId, string? code, CancellationToken ct = default);
    /// <summary>The business's own referral code (created on first use), with its results.</summary>
    Task<CustomerReferral> GetCustomerReferralAsync(int businessId, CancellationToken ct = default);
}
