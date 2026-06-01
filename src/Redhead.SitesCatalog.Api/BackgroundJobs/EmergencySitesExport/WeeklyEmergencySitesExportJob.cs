using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.SystemJobs;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.BackgroundJobs.EmergencySitesExport;

public sealed class WeeklyEmergencySitesExportJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyEmergencySitesExportJob> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public WeeklyEmergencySitesExportJob(
        IServiceScopeFactory scopeFactory,
        ILogger<WeeklyEmergencySitesExportJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<WeeklyEmergencySitesExportJobResult> RunIfDueAsync(
        EmergencySitesExportCronSchedule schedule,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var dueOccurrenceUtc = schedule.GetDueOccurrenceUtc(utcNow);
        if (dueOccurrenceUtc is null)
        {
            _logger.LogInformation(
                "Weekly emergency Sites export skipped because no scheduled occurrence is due in the current weekly period.");
            return WeeklyEmergencySitesExportJobResult.NotDue();
        }

        var periodKey = SystemJobPeriodKey.FromUtcWeek(dueOccurrenceUtc.Value);
        if (await HasSuccessfulRunAsync(periodKey, cancellationToken))
        {
            _logger.LogInformation(
                "Weekly emergency Sites export skipped because this period already succeeded. PeriodKey={PeriodKey}",
                periodKey);
            return WeeklyEmergencySitesExportJobResult.AlreadyCompleted(periodKey, dueOccurrenceUtc.Value);
        }

        if (!await _runLock.WaitAsync(0, cancellationToken))
        {
            _logger.LogInformation(
                "Weekly emergency Sites export skipped because a previous run is still active. PeriodKey={PeriodKey}",
                periodKey);
            return WeeklyEmergencySitesExportJobResult.AlreadyRunning(periodKey, dueOccurrenceUtc.Value);
        }

        try
        {
            return await RunExportAsync(periodKey, dueOccurrenceUtc.Value, cancellationToken);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task<WeeklyEmergencySitesExportJobResult> RunExportAsync(
        string periodKey,
        DateTime dueOccurrenceUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var jobRunService = scope.ServiceProvider.GetRequiredService<ISystemJobRunService>();
            if (await jobRunService.HasSuccessfulRunAsync(
                SystemJobNames.WeeklySitesEmergencyExport,
                periodKey,
                cancellationToken))
            {
                _logger.LogInformation(
                    "Weekly emergency Sites export skipped after lock because this period already succeeded. PeriodKey={PeriodKey}",
                    periodKey);
                return WeeklyEmergencySitesExportJobResult.AlreadyCompleted(periodKey, dueOccurrenceUtc);
            }

            _logger.LogInformation(
                "Weekly emergency Sites export started. PeriodKey={PeriodKey}; DueOccurrenceUtc={DueOccurrenceUtc}",
                periodKey,
                dueOccurrenceUtc);

            var runner = scope.ServiceProvider.GetRequiredService<IEmergencySitesExportRunner>();
            var result = await runner.RunOnceAsync(dueOccurrenceUtc, cancellationToken);
            if (result.Skipped)
            {
                _logger.LogInformation(
                    "Weekly emergency Sites export skipped by runner because this period already succeeded. PeriodKey={PeriodKey}",
                    result.PeriodKey);
                return WeeklyEmergencySitesExportJobResult.AlreadyCompleted(
                    result.PeriodKey,
                    dueOccurrenceUtc);
            }

            _logger.LogInformation(
                "Weekly emergency Sites export succeeded. PeriodKey={PeriodKey}; RunId={RunId}; FileName={FileName}; DeletedOldFiles={DeletedOldFiles}; FailedOldFileDeletes={FailedOldFileDeletes}",
                result.PeriodKey,
                result.RunId,
                result.UploadedFile?.FileName,
                result.CleanupResult?.DeletedCount,
                result.CleanupResult?.FailedCount);

            return WeeklyEmergencySitesExportJobResult.Completed(result.PeriodKey, dueOccurrenceUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Weekly emergency Sites export failed. PeriodKey={PeriodKey}",
                periodKey);
            return WeeklyEmergencySitesExportJobResult.Failed(periodKey, dueOccurrenceUtc);
        }
    }

    private async Task<bool> HasSuccessfulRunAsync(
        string periodKey,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobRunService = scope.ServiceProvider.GetRequiredService<ISystemJobRunService>();
        return await jobRunService.HasSuccessfulRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            cancellationToken);
    }
}
