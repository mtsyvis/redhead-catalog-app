namespace Redhead.SitesCatalog.Application.Exports;

public sealed record EmergencySitesExportResult(
    string FileName,
    string ContentType,
    Stream FileStream,
    int RowCount,
    long FileSizeBytes);
