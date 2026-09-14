using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogActivityCleanup;

public sealed class ClientCatalogActivityCleanupHostedService(
    IServiceScopeFactory scopes,
    ILogger<ClientCatalogActivityCleanupHostedService> logger,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-30);
                await db.ClientCatalogRequests.Where(x => x.TimestampUtc < cutoff).ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Could not remove expired Client catalog activity");
            }
        }
    }
}
