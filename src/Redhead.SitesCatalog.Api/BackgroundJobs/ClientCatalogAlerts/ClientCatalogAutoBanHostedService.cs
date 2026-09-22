using Redhead.SitesCatalog.Application.Services.ClientCatalog;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogAlerts;

public sealed class ClientCatalogAutoBanHostedService(
    IServiceScopeFactory scopes,
    ILogger<ClientCatalogAutoBanHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ClientCatalogAutoBanService.ScanInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ClientCatalogAutoBanService>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not process Client catalog automatic bans"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
