namespace Redhead.SitesCatalog.Application.Services;

public record ExportResult(
    Stream CsvStream,
    int RequestedRows,
    int ExportedRows,
    bool Truncated,
    int? LimitRows);
