using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;

public sealed class WeeklyEmergencySitesExportHostedService : BackgroundService
{
    internal static readonly TimeSpan FailureRetryDelay = TimeSpan.FromHours(1);

    private readonly WeeklyEmergencySitesExportJob _job;
    private readonly EmergencySitesExportOptions _options;
    private readonly ILogger<WeeklyEmergencySitesExportHostedService> _logger;

    public WeeklyEmergencySitesExportHostedService(
        WeeklyEmergencySitesExportJob job,
        IOptions<EmergencySitesExportOptions> options,
        ILogger<WeeklyEmergencySitesExportHostedService> logger)
    {
        _job = job;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Weekly emergency Sites export is disabled.");
            return;
        }

        if (!EmergencySitesExportCronSchedule.TryParse(_options.ScheduleCron, out var schedule))
        {
            _logger.LogCritical(
                "Weekly emergency Sites export schedule is invalid: {ScheduleCron}",
                _options.ScheduleCron);
            return;
        }

        _logger.LogInformation(
            "Weekly emergency Sites export scheduler started. ScheduleCron={ScheduleCron}; TimeZone=UTC",
            _options.ScheduleCron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var result = await _job.RunIfDueAsync(schedule, stoppingToken);
            var delay = GetNextDelay(schedule, result, DateTime.UtcNow);

            _logger.LogInformation(
                "Weekly emergency Sites export scheduler sleeping. Status={Status}; PeriodKey={PeriodKey}; Delay={Delay}",
                result.Status,
                result.PeriodKey,
                delay);

            await Task.Delay(delay, stoppingToken);
        }
    }

    private static TimeSpan GetNextDelay(
        EmergencySitesExportCronSchedule schedule,
        WeeklyEmergencySitesExportJobResult result,
        DateTime utcNow)
    {
        if (result.Status == WeeklyEmergencySitesExportJobStatus.Failed)
        {
            return FailureRetryDelay;
        }

        var nextOccurrenceUtc = schedule.GetNextOccurrenceUtc(utcNow);
        if (nextOccurrenceUtc is null)
        {
            return FailureRetryDelay;
        }

        var delay = nextOccurrenceUtc.Value - utcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }
}
