namespace Redhead.SitesCatalog.Domain.Entities;

public class GoogleDriveConnection
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? GoogleSubjectId { get; set; }
    public string? GoogleEmail { get; set; }
    public string RefreshTokenEncrypted { get; set; } = string.Empty;
    public string GrantedScopes { get; set; } = string.Empty;
    public string? ExportFolderId { get; set; }
    public string? ExportFolderName { get; set; }
    public DateTime ConnectedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? LastError { get; set; }
}
