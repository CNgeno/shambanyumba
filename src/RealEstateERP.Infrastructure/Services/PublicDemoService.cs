using RealEstateERP.Core.Interfaces;
using RealEstateERP.Infrastructure.Data;

namespace RealEstateERP.Infrastructure.Services;

public sealed class PublicDemoService(IServiceProvider services, ApplicationDbContext db) : IPublicDemoService
{
    public async Task<(string OwnerUserId, string AgentUserId)> EnsureAsync(CancellationToken ct = default)
    {
        var (owner, agent) = await PublicDemo.EnsureAsync(services);
        return (owner.Id, agent.Id);
    }

    public Task ResetAsync(CancellationToken ct = default) => PublicDemo.ResetAsync(db, ct);
}
