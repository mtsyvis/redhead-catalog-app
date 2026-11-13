using Redhead.SitesCatalog.Application.Services.Currencies;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.Currencies;

public sealed class CurrencyReferenceHostedService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<CurrencyReferenceHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<CurrencyReferenceRefreshService>().TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError("Currency reference refresh failed ({ExceptionType}).", exception.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
