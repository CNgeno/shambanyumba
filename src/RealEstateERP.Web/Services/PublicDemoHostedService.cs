using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Web.Services;

/// <summary>
/// Keeps the public demo available and clean: creates it at startup if missing, and resets it every night at
/// 03:00 EAT (everything visitors changed is wiped and the full journeys are seeded again).
/// Settings: Demo:PublicEnabled (default true), Demo:ResetHourEat (default 3).
/// </summary>
public sealed class PublicDemoHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<PublicDemoHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Demo:PublicEnabled", true)) return;
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { return; }

        await RunAsync(reset: false, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var nowEat = DateTime.UtcNow.AddHours(3);
            var next = nowEat.Date.AddHours(config.GetValue("Demo:ResetHourEat", 3));
            if (next <= nowEat) next = next.AddDays(1);
            try { await Task.Delay(next - nowEat, stoppingToken); } catch (OperationCanceledException) { return; }
            await RunAsync(reset: true, stoppingToken);
        }
    }

    private async Task RunAsync(bool reset, CancellationToken ct)
    {
        try
        {
            using (var scope = scopes.CreateScope())
                if (reset) await scope.ServiceProvider.GetRequiredService<IPublicDemoService>().ResetAsync(ct);
            using (var scope = scopes.CreateScope())   // a fresh context after the raw-SQL reset
                await scope.ServiceProvider.GetRequiredService<IPublicDemoService>().EnsureAsync(ct);
            logger.LogInformation(reset ? "Public demo reset." : "Public demo ready.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Public demo {Action} failed.", reset ? "reset" : "setup");
        }
    }
}
