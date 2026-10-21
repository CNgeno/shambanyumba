using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

public interface ISubscriptionService
{
    /// <summary>Adds the trial period for a NEW business to the context. Caller saves (same transaction).</summary>
    Task AddTrialAsync(Business business, CancellationToken ct = default);

    Task<SubscriptionSnapshot> GetSnapshotAsync(int businessId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetAvailablePlansAsync(int businessId, CancellationToken ct = default);

    // Business side
    Task<Result<int>> SubmitRenewalAsync(int businessId, RenewalRequest request, CancellationToken ct = default);
    Task<SubscriptionPayment?> GetPendingPaymentAsync(int businessId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPayment>> GetPaymentsAsync(int businessId, CancellationToken ct = default);

    // Platform admin side
    Task<IReadOnlyList<SubscriptionPayment>> GetPendingPaymentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPayment>> GetRecentlyReviewedPaymentsAsync(int take, CancellationToken ct = default);
    Task<SubscriptionPayment?> GetPaymentAsync(int paymentId, CancellationToken ct = default);
    Task<Result> ApproveAsync(int paymentId, string reviewerUserId, string? note, CancellationToken ct = default);
    Task<Result> RejectAsync(int paymentId, string reviewerUserId, string note, CancellationToken ct = default);
}
