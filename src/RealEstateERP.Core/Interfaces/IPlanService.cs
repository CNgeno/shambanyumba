using RealEstateERP.Core.Common;
using RealEstateERP.Core.Entities;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Core.Interfaces;

public interface IPlanService
{
    Task<IReadOnlyList<SubscriptionPlan>> GetAllAsync(CancellationToken ct = default);
    Task<SubscriptionPlan?> GetAsync(int id, CancellationToken ct = default);
    Task<Result<int>> SaveAsync(SubscriptionPlanInput input, CancellationToken ct = default);
}
