using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.ExportedDomainAccessCleanup;

public sealed class ExportedDomainAccessCleanupHostedService : BackgroundService
{
    internal static readonly TimeSpan FailureRetryDelay = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExportedDomainAccessCleanupOptions _options;
    private readonly ILogger<ExportedDomainAccessCleanupHostedService> _logger;

    public ExportedDomainAccessCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ExportedDomainAccessCleanupOptions> options,
        ILogger<ExportedDomainAccessCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Exported domain access cleanup is disabled.");
            return;
        }

        _logger.LogInformation(
            "Exported domain access cleanup started. RetentionDays={RetentionDays}; BatchSize={BatchSize}; IntervalHours={IntervalHours}",
            _options.RetentionDays,
            _options.BatchSize,
            _options.IntervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = await RunCleanupAndGetDelayAsync(stoppingToken);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<TimeSpan> RunCleanupAndGetDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deletedCount = await RunCleanupAsync(cancellationToken);
            _logger.LogInformation(
                "Exported domain access cleanup completed. DeletedRows={DeletedRows}",
                deletedCount);

            return TimeSpan.FromHours(_options.IntervalHours);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exported domain access cleanup failed.");
            return FailureRetryDelay;
        }
    }

    private async Task<int> RunCleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<IExportedDomainAccessCleanupService>();
        var cutoffUtc = DateTime.UtcNow.AddDays(-_options.RetentionDays);

        return await cleanupService.DeleteOldAccessesAsync(
            cutoffUtc,
            _options.BatchSize,
            cancellationToken);
    }
}
