using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.SystemJobs;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.SystemExports;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed class EmergencySitesExportRunner : IEmergencySitesExportRunner
{
    private readonly IEmergencySitesExportService _exportService;
    private readonly ISystemExportStorage _storage;
    private readonly ISystemJobRunService _systemJobRunService;
    private readonly EmergencySitesExportOptions _options;

    public EmergencySitesExportRunner(
        IEmergencySitesExportService exportService,
        ISystemExportStorage storage,
        ISystemJobRunService systemJobRunService,
        IOptions<EmergencySitesExportOptions> options)
    {
        _exportService = exportService;
        _storage = storage;
        _systemJobRunService = systemJobRunService;
        _options = options.Value;
    }

    public async Task<EmergencySitesExportRunResult> RunOnceAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Emergency Sites export is disabled.");
        }

        if (!EmergencySitesExportOptions.IsValid(_options))
        {
            throw new InvalidOperationException(
                "EmergencySitesExport configuration is invalid. Enabled exports require ScheduleCron, GoogleDriveFolderId, ServiceAccountJsonPath, FilePrefix, positive RetentionWeeks, and positive UploadTimeoutMinutes values.");
        }

        var now = utcNow ?? DateTime.UtcNow;
        var periodKey = SystemJobPeriodKey.FromUtcWeek(now);
        if (await _systemJobRunService.HasSuccessfulRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            cancellationToken))
        {
            return new EmergencySitesExportRunResult(
                RunId: null,
                periodKey,
                Skipped: true,
                UploadedFile: null,
                CleanupResult: null);
        }

        var run = await _systemJobRunService.StartRunAsync(
            SystemJobNames.WeeklySitesEmergencyExport,
            periodKey,
            cancellationToken);

        try
        {
            var export = await _exportService.GenerateAsync(
                GetFilePrefix(_options),
                cancellationToken);
            await using var exportStream = export.FileStream;

            var uploaded = await _storage.UploadAsync(
                export.FileName,
                export.ContentType,
                exportStream,
                cancellationToken);

            await _systemJobRunService.AddArtifactAsync(
                run.Id,
                new SystemJobArtifactInput(
                    uploaded.FileName,
                    uploaded.FileSizeBytes,
                    uploaded.StorageProvider,
                    uploaded.StoragePath,
                    uploaded.ExternalFileId),
                cancellationToken);

            var cleanup = await _storage.DeleteOldFilesAsync(
                GetFilePrefix(_options),
                TimeSpan.FromDays(_options.RetentionWeeks * 7),
                cancellationToken);

            await _systemJobRunService.MarkSucceededAsync(run.Id, cancellationToken);

            return new EmergencySitesExportRunResult(
                run.Id,
                periodKey,
                Skipped: false,
                uploaded,
                cleanup);
        }
        catch (Exception ex)
        {
            await _systemJobRunService.MarkFailedAsync(
                run.Id,
                ex.Message,
                cancellationToken);
            throw;
        }
    }

    private static string GetFilePrefix(EmergencySitesExportOptions options)
        => string.IsNullOrWhiteSpace(options.FilePrefix)
            ? EmergencySitesExportOptions.DefaultFilePrefix
            : options.FilePrefix.Trim();
}
