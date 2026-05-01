namespace Redhead.SitesCatalog.Application.Services;

public record ExportResult(
    Stream FileStream,
    int RequestedRows,
    int ExportedRows,
    bool Truncated,
    int? LimitRows);
