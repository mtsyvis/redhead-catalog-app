namespace Redhead.SitesCatalog.Api.Models;

public sealed record GoogleDriveExportResponse(
    string FileId,
    string FileName,
    string? WebViewLink,
    int RowsExported,
    bool WasTruncated,
    DateTime ExportedAtUtc,
    string DestinationLabel);

