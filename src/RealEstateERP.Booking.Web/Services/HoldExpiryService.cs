using RealEstateERP.Core.Interfaces;

namespace RealEstateERP.Booking.Web.Services;

/// <summary>Every 5 minutes: releases guest-site bookings whose deposit didn't arrive within the hold (BookingRules.DepositHold).</summary>
public sealed class HoldExpiryService(IServiceScopeFactory scopes, ILogger<HoldExpiryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var released = await scope.ServiceProvider.GetRequiredService<IStaySearchService>().ExpireHoldsAsync(stoppingToken);
                if (released > 0) logger.LogInformation("Released {Count} unpaid booking hold(s).", released);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Releasing expired booking holds failed.");
            }
            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}
