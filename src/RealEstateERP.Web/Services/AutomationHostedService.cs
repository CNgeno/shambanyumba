using RealEstateERP.Core.Enums;
using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Services;

/// <summary>
/// The daily automation job. Every hour it looks for businesses with automation switched on whose subscription is
/// usable, and runs each once per day (claimed with the business row's rowversion, so two servers never both run it).
/// Each business runs in its own DI scope inside a BusinessScope; one business failing never stops the others.
/// </summary>
public sealed class AutomationHostedService(IServiceScopeFactory scopes, ILogger<AutomationHostedService> logger, IConfiguration config) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Automation:Enabled", true))
        {
            logger.LogInformation("Automation job is disabled (Automation:Enabled = false).");
            return;
        }

        // Let the app finish starting (and migrating) first.
        try { await Task.Delay(TimeSpan.FromMinutes(config.GetValue("Automation:StartDelayMinutes", 2)), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(config.GetValue("Automation:IntervalMinutes", 60)));
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Automation sweep failed."); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<int> ids;
        using (var listing = scopes.CreateScope())
            ids = await listing.ServiceProvider.GetRequiredService<IAutomationService>().GetAutomatedBusinessIdsAsync(ct);

        foreach (var businessId in ids)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sp = scope.ServiceProvider;
                var snapshot = await sp.GetRequiredService<ISubscriptionService>().GetSnapshotAsync(businessId, ct);
                if (snapshot.Status == SubscriptionStatus.Suspended) continue;   // suspended businesses get no automatic work

                // Business type only affects agent restrictions; the job acts as Owner, which is never restricted.
                using (BusinessScope.Begin(businessId, null))
                {
                    var automation = sp.GetRequiredService<IAutomationService>();
                    if (!await automation.TryClaimDailyRunAsync(ct)) continue;
                    var result = await automation.RunAsync(ct);
                    logger.LogInformation("Automation for business {BusinessId}: {Summary}", businessId, result.Summary);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Automation failed for business {BusinessId}.", businessId);
            }
        }
    }
}
