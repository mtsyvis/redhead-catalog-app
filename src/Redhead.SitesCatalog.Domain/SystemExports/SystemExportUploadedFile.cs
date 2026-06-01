namespace Redhead.SitesCatalog.Domain.SystemExports;

public sealed record SystemExportUploadedFile(
    string FileName,
    long FileSizeBytes,
    string StorageProvider,
    string StoragePath,
    string ExternalFileId,
    string? WebViewLink);
