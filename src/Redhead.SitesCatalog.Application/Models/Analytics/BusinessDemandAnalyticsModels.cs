namespace Redhead.SitesCatalog.Application.Models.Analytics;

public static class BusinessDemandAnalyticsStatuses
{
    public const string Successful = "successful";
    public const string Partial = "partial";
    public const string Blocked = "blocked";
}

public sealed class BusinessDemandAnalyticsQuery
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
    public string? ClientId { get; init; }
    public string? Destination { get; init; }
    public string? Status { get; init; }
}

public sealed record BusinessDemandAnalyticsDto(
    BusinessDemandSummaryDto Summary,
    IReadOnlyList<BusinessDemandCountDto> TopLocations,
    IReadOnlyList<BusinessDemandCountDto> TopNiches,
    IReadOnlyList<BusinessDemandCountDto> TopCategories,
    IReadOnlyList<BusinessDemandCountDto> TopLanguages,
    IReadOnlyList<ServiceDemandDto> ServiceDemand,
    QualityDemandDto QualityDemand,
    FilterStrictnessDto FilterStrictness);

public sealed record BusinessDemandSummaryDto(
    int ExportRequests,
    int ClientsWithExportActivity,
    long RequestedRows,
    long ExportedDomains);

public sealed record BusinessDemandCountDto(string Name, int ExportRequests);

public sealed record ServiceDemandDto(
    string Service,
    int WantedOrAvailableRequests,
    int ExplicitlyNoRequests);

public sealed record QualityDemandDto(
    IReadOnlyList<BusinessDemandCountDto> DrRanges,
    IReadOnlyList<BusinessDemandCountDto> TrafficRanges,
    IReadOnlyList<BusinessDemandCountDto> PriceRanges);

public sealed record FilterStrictnessDto(
    int NoFilters,
    int BroadExports,
    int FilteredExports,
    int BroadExportThreshold);

public sealed record AnalyticsClientOptionDto(
    string Id,
    string Email,
    string DisplayName);
