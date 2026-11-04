using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.LinkDetective;

public sealed class LinkDetectiveSyncHostedService(
    IServiceScopeFactory scopes, IOptions<LinkDetectiveSyncOptions> options,
    TimeProvider clock, ILogger<LinkDetectiveSyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(1);
            try
            {
                using var scope = scopes.CreateScope();
                var next = await scope.ServiceProvider.GetRequiredService<LinkDetectiveSyncService>().TickAsync(stoppingToken);
                // Recheck persisted state at least once a minute, even during long quota waits.
                delay = TimeSpan.FromSeconds(Math.Clamp((next - clock.GetUtcNow().UtcDateTime).TotalSeconds, 2, 60));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                // Exception contents can contain database/provider details; do not log them or domains.
                logger.LogError("LinkDetective background sync failed ({ExceptionType}); it will retry from saved progress.", exception.GetType().Name);
            }
            try { await Task.Delay(delay, clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
