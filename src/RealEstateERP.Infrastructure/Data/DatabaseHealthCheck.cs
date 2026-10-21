using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RealEstateERP.Infrastructure.Data;

/// <summary>
/// /health: Unhealthy when SQL Server can't be reached; Degraded when migrations are pending (the app runs but
/// pages using new tables will fail). Used by Azure App Service health checks.
/// </summary>
public sealed class DatabaseHealthCheck(ApplicationDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Database unreachable.");

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        return pending.Count == 0
            ? HealthCheckResult.Healthy("Database reachable and up to date.")
            : HealthCheckResult.Degraded($"{pending.Count} database migration(s) not applied: {string.Join(", ", pending)}.");
    }
}
