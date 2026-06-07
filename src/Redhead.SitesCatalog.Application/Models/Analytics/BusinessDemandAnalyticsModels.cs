namespace Redhead.SitesCatalog.Application.Models.Analytics;

public sealed class BusinessDemandAnalyticsQuery : AnalyticsQuery;

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
