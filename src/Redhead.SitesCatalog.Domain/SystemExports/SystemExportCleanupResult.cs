namespace Redhead.SitesCatalog.Domain.SystemExports;

public sealed record SystemExportCleanupResult(
    int DeletedCount,
    int FailedCount);
