namespace Redhead.SitesCatalog.Application.Models.Analytics;

public sealed class ExportActivityAnalyticsQuery : AnalyticsQuery
{
    public DateTime NowUtc { get; init; }
    public int RecentExportsPage { get; init; }
    public int RecentExportsPageSize { get; init; }
}

public sealed record ExportActivityAnalyticsDto(
    ExportActivitySummaryDto Summary,
    IReadOnlyList<ExportActivityOverTimeDto> ExportsOverTime,
    IReadOnlyList<ExportActivityClientSummaryDto> ClientSummaries,
    ExportActivityRecentExportsDto RecentExports);

public sealed record ExportActivitySummaryDto(
    int CompletedExports,
    int PartialExports,
    int BlockedExports,
    int UniqueExportedDomains,
    long RequestedRows,
    long ExportedRows);

public sealed record ExportActivityOverTimeDto(
    string Date,
    int SuccessfulExports,
    int PartialExports,
    int BlockedExports,
    int ExportedDomains);

public sealed record ExportActivityClientSummaryDto(
    string UserId,
    string Email,
    string? DisplayName,
    int SuccessfulExports,
    int PartialExports,
    int BlockedExports,
    long RequestedRows,
    long ExportedRows,
    DateTime? LastExportAtUtc);

public sealed record ExportActivityRecentExportsDto(
    IReadOnlyList<ExportActivityRecentExportDto> Items,
    int TotalCount);

public sealed record ExportActivityRecentExportDto(
    Guid Id,
    DateTime TimestampUtc,
    string UserId,
    string Email,
    string? DisplayName,
    string? Destination,
    string Status,
    int RequestedRows,
    int ExportedRows,
    string? BlockedReason,
    string? Reason,
    string? FiltersSummary,
    string? SortSummary);
