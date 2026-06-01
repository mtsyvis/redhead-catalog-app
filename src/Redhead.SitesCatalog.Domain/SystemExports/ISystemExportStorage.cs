namespace Redhead.SitesCatalog.Domain.SystemExports;

public interface ISystemExportStorage
{
    Task<SystemExportUploadedFile> UploadAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<SystemExportCleanupResult> DeleteOldFilesAsync(
        string fileNamePrefix,
        TimeSpan retention,
        CancellationToken cancellationToken = default);
}
