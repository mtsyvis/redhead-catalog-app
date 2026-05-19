namespace Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

public sealed record GoogleDriveStatusResponse(
    bool Connected,
    string? GoogleEmail,
    DateTime? ConnectedAtUtc,
    string? ExportFolderName,
    bool HasExportFolderId,
    bool Revoked,
    bool NeedsReconnect);

public sealed record GoogleDriveConnectStartResponse(string AuthorizationUrl);
