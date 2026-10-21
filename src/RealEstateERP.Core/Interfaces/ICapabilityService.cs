namespace RealEstateERP.Core.Interfaces;

/// <summary>
/// What the current business uses, decided by its properties and the agency setting (not by a business type):
/// Rentals when any property is to let, Sales when any is for sale, Agency when it manages for other owners
/// and its plan allows it.
/// </summary>
public sealed record Capabilities(bool HasProperties, bool Rentals, bool Sales, bool AgencyOn, bool AgencyAllowedByPlan)
{
    public bool Agency => AgencyOn && AgencyAllowedByPlan;
    public static readonly Capabilities None = new(false, false, false, false, false);
}

/// <summary>Cached per business for a few minutes; cleared when properties or the agency setting change. Depends on: DbContext, ISubscriptionService, IMemoryCache.</summary>
public interface ICapabilityService
{
    Task<Capabilities> GetAsync(CancellationToken ct = default);
    void Invalidate(int businessId);

    /// <summary>Can the signed-in member see finances (reports, costs, expenses, net income)? Owners and Admins always.</summary>
    Task<bool> CanViewFinancialsAsync(CancellationToken ct = default);
    void InvalidateMember(int businessId, string userId);
}
