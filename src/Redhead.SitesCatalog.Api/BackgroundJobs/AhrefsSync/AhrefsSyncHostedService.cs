using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Ahrefs;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.AhrefsSync;

public sealed class AhrefsSyncHostedService : BackgroundService
{
    private static readonly TimeSpan FailureDelay = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AhrefsSyncOptions _options;
    private readonly ILogger<AhrefsSyncHostedService> _logger;

    public AhrefsSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AhrefsSyncOptions> options,
        ILogger<AhrefsSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Ahrefs monthly sync is disabled.");
            return;
        }

        if (!AhrefsSyncCronSchedule.TryParse(_options.Cron, out var schedule))
        {
            _logger.LogCritical("Ahrefs monthly sync cron is invalid.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var due = schedule.GetDueOccurrenceUtc(now);
            if (due.HasValue)
            {
                var shouldRetry = await RunScheduledAsync(stoppingToken);
                if (shouldRetry)
                {
                    await Task.Delay(FailureDelay, stoppingToken);
                    continue;
                }
            }

            var next = schedule.GetNextOccurrenceUtc(DateTime.UtcNow);
            var delay = next.HasValue
                ? next.Value - DateTime.UtcNow
                : FailureDelay;
            await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task<bool> RunScheduledAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IAhrefsSyncService>();
            var result = await service.RunAsync(
                new AhrefsSyncRequest(
                    AhrefsSyncRunKind.Scheduled,
                    null,
                    null,
                    SaveSnapshots: true,
                    Force: false),
                cancellationToken);
            _logger.LogInformation(
                "Ahrefs scheduled sync finished. Conflict={Conflict}; WaitingForUsageReset={WaitingForUsageReset}; RunId={RunId}; Status={Status}",
                result.Conflict,
                result.WaitingForUsageReset,
                result.Run?.Id,
                result.Run?.Status);
            return ShouldRetry(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ahrefs scheduled sync invocation failed.");
            return true;
        }
    }

    public static bool ShouldRetry(AhrefsSyncRunResult result)
        => result.Conflict ||
            result.WaitingForUsageReset ||
            result.Run?.Status == AhrefsSyncRunStatus.Failed;
}
