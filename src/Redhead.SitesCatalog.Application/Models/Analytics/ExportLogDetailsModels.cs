namespace Redhead.SitesCatalog.Application.Models.Analytics;

public sealed record ExportLogDetailsDto(
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
    string? OutcomeReason,
    string? ExportMode,
    IReadOnlyList<ExportLogDetailsSectionDto> AppliedFilters,
    ExportLogSortDetailsDto Sort,
    ExportLogTechnicalDetailsDto? TechnicalDetails);

public sealed record ExportLogDetailsSectionDto(
    string Title,
    IReadOnlyList<ExportLogDetailsRowDto> Rows);

public sealed record ExportLogDetailsRowDto(
    string Label,
    string Value);

public sealed record ExportLogSortDetailsDto(
    string Summary,
    IReadOnlyList<ExportLogDetailsRowDto> Items);

public sealed record ExportLogTechnicalDetailsDto(
    string? FiltersSnapshotJson,
    string? SortSnapshotJson,
    string? SearchSnapshotJson);
