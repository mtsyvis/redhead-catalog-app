namespace Redhead.SitesCatalog.Domain.Entities;

public class SystemJobArtifact
{
    public Guid Id { get; set; }
    public Guid SystemJobRunId { get; set; }
    public SystemJobRun? SystemJobRun { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? StorageProvider { get; set; }
    public string? StoragePath { get; set; }
    public string? ExternalFileId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
