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
    private readonly Func<DateTime> _utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public AhrefsSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AhrefsSyncOptions> options,
        ILogger<AhrefsSyncHostedService> logger)
        : this(
            scopeFactory,
            options,
            logger,
            () => DateTime.UtcNow,
            static (delay, cancellationToken) => Task.Delay(delay, cancellationToken))
    {
    }

    internal AhrefsSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<AhrefsSyncOptions> options,
        ILogger<AhrefsSyncHostedService> logger,
        Func<DateTime> utcNow,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _utcNow = utcNow;
        _delay = delay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        => await RunLoopAsync(stoppingToken);

    internal async Task RunLoopAsync(CancellationToken stoppingToken)
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
            var now = _utcNow();
            var notBeforeUtc = _options.NotBeforeUtc?.UtcDateTime;
            if (notBeforeUtc.HasValue && now < notBeforeUtc.Value)
            {
                await _delay(notBeforeUtc.Value - now, stoppingToken);
                continue;
            }

            var due = schedule.GetDueOccurrenceUtc(now);
            if (due.HasValue)
            {
                var shouldRetry = await RunScheduledAsync(stoppingToken);
                if (shouldRetry)
                {
                    await _delay(FailureDelay, stoppingToken);
                    continue;
                }
            }

            var currentUtc = _utcNow();
            var next = schedule.GetNextOccurrenceUtc(currentUtc);
            var delay = next.HasValue
                ? next.Value - currentUtc
                : FailureDelay;
            await _delay(
                delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1),
                stoppingToken);
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
