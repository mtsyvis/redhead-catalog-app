namespace Redhead.SitesCatalog.Application.Models.Exports;

public record ExportResult(
    Stream FileStream,
    int RequestedRows,
    int ExportedRows,
    bool Truncated,
    int? LimitRows,
    string? TruncationReason = null);
