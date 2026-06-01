using Redhead.SitesCatalog.Domain.SystemExports;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed record EmergencySitesExportRunResult(
    Guid? RunId,
    string PeriodKey,
    bool Skipped,
    SystemExportUploadedFile? UploadedFile,
    SystemExportCleanupResult? CleanupResult);
