namespace Redhead.SitesCatalog.Application.Models.Analytics;

public static class ExportActivityClientUsageStatuses
{
    public const string Normal = "Normal";
    public const string NearLimit = "NearLimit";
    public const string LimitReached = "LimitReached";
}

public sealed class ExportActivityAnalyticsQuery : AnalyticsQuery
{
    public DateTime NowUtc { get; init; }
    public int RecentExportsPage { get; init; }
    public int RecentExportsPageSize { get; init; }
}

public sealed record ExportActivityAnalyticsDto(
    ExportActivitySummaryDto Summary,
    IReadOnlyList<ExportActivityOverTimeDto> ExportsOverTime,
    IReadOnlyList<ExportActivityClientUsageDto> ClientUsage,
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

public sealed record ExportActivityClientUsageDto(
    string UserId,
    string Email,
    string? DisplayName,
    int DailyUniqueDomainsUsed,
    int? DailyUniqueDomainsLimit,
    int WeeklyUniqueDomainsUsed,
    int? WeeklyUniqueDomainsLimit,
    int DailyExportOperationsUsed,
    int? DailyExportOperationsLimit,
    int WeeklyExportOperationsUsed,
    int? WeeklyExportOperationsLimit,
    int PartialExports,
    int BlockedExports,
    long RequestedRows,
    long ExportedRows,
    DateTime? LastExportAtUtc,
    string Status);

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
