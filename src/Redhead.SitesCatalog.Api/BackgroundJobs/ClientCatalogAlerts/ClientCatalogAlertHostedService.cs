using Redhead.SitesCatalog.Application.Services.ClientCatalog;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.ClientCatalogAlerts;

public sealed class ClientCatalogAlertHostedService(IServiceScopeFactory scopes, ILogger<ClientCatalogAlertHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ClientCatalogAlertService.ScanInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ClientCatalogAlertService>().ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning(exception, "Could not process Client catalog alerts"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
