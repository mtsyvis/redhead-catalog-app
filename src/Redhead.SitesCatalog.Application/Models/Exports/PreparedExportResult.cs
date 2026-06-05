using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Models.Exports;

public sealed record PreparedExportResult(
    Stream FileStream,
    int RequestedRows,
    int ExportedRows,
    bool Truncated,
    int? LimitRows,
    string? TruncationReason,
    ExportLog ExportLog,
    ExportAnalyticsSnapshot? AnalyticsSnapshot,
    IReadOnlyList<ExportedDomainAccess> ExportedDomainAccesses)
{
    public ExportResult ToExportResult()
        => new(
            FileStream,
            RequestedRows,
            ExportedRows,
            Truncated,
            LimitRows,
            TruncationReason);
}
