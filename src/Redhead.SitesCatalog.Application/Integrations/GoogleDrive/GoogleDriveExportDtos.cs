namespace Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

public sealed record GoogleDriveExportResponse(
    string FileId,
    string FileName,
    string? WebViewLink,
    int RowsExported,
    bool WasTruncated,
    DateTime ExportedAtUtc,
    string DestinationLabel,
    string? TruncationReason = null);
