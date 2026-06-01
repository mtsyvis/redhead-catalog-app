namespace Redhead.SitesCatalog.Application.SystemJobs;

public sealed record SystemJobArtifactInput(
    string FileName,
    long FileSizeBytes,
    string? StorageProvider = null,
    string? StoragePath = null,
    string? ExternalFileId = null);
