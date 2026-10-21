namespace RealEstateERP.Core.Interfaces;

/// <summary>The public demo: two logins and three demo businesses, reset every night. Depends on: DbContext, UserManager.</summary>
public interface IPublicDemoService
{
    /// <summary>Creates whatever is missing; returns the user ids of the demo owner and the demo agent.</summary>
    Task<(string OwnerUserId, string AgentUserId)> EnsureAsync(CancellationToken ct = default);
    /// <summary>Deletes everything belonging to demo businesses (then call EnsureAsync for a clean copy).</summary>
    Task ResetAsync(CancellationToken ct = default);
}
