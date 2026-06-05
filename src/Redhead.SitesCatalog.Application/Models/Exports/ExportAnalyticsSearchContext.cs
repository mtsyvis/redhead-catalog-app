namespace Redhead.SitesCatalog.Application.Models.Exports;

public sealed record ExportAnalyticsSearchContext(
    string Mode,
    int? InputCount = null,
    int? UniqueInputCount = null,
    int? FoundCount = null,
    int? NotFoundCount = null);
