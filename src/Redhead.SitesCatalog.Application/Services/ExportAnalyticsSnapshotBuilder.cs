using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Exports;
using Redhead.SitesCatalog.Application.Services.Analytics;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services;

public static class ExportAnalyticsSnapshotBuilder
{
    public const int CurrentSnapshotVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static ExportAnalyticsSnapshot Create(
        ExportLog exportLog,
        SitesQuery query,
        ExportAnalyticsSearchContext? searchContext = null)
    {
        return new ExportAnalyticsSnapshot
        {
            Id = Guid.NewGuid(),
            ExportLogId = exportLog.Id,
            ExportLog = exportLog,
            SnapshotVersion = CurrentSnapshotVersion,
            FiltersSnapshotJson = JsonSerializer.Serialize(CreateFiltersSnapshot(query), JsonOptions),
            SortSnapshotJson = JsonSerializer.Serialize(CreateSortSnapshot(query), JsonOptions),
            SearchSnapshotJson = CreateSearchSnapshotJson(query, searchContext),
            CreatedAtUtc = exportLog.TimestampUtc
        };
    }

    public static ExportAnalyticsSearchContext CreateMultiSearchContext(
        int inputCount,
        int uniqueInputCount,
        int foundCount)
    {
        return new ExportAnalyticsSearchContext(
            Mode: ExportAnalyticsSnapshotSchema.Search.MultiSearchMode,
            InputCount: inputCount,
            UniqueInputCount: uniqueInputCount,
            FoundCount: foundCount,
            NotFoundCount: uniqueInputCount - foundCount);
    }

    private static FiltersSnapshotDto CreateFiltersSnapshot(SitesQuery query)
    {
        var filters = new List<FilterSnapshotItemDto>();

        AddNumberRange(filters, ExportAnalyticsSnapshotSchema.Filters.Dr, query.DrMin, query.DrMax);
        AddNumberRange(filters, ExportAnalyticsSnapshotSchema.Filters.Traffic, query.TrafficMin, query.TrafficMax);
        AddNumberRange(filters, ExportAnalyticsSnapshotSchema.Filters.PriceUsd, query.PriceMin, query.PriceMax);
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.Location, query.Locations);
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.LocationKey, query.LocationKeys);
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.LocationGroup, query.LocationGroupKeys);
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.ExcludedLocationKey, query.ExcludedLocationKeys);
        if (query.IncludeUnknownLocation)
        {
            filters.Add(new FilterSnapshotItemDto(ExportAnalyticsSnapshotSchema.Filters.LocationUnknown, "boolean", "eq", true));
        }

        if (query.IncludeOtherLocation)
        {
            filters.Add(new FilterSnapshotItemDto(ExportAnalyticsSnapshotSchema.Filters.LocationOther, "boolean", "eq", true));
        }

        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.Language, query.Languages);

        var niches = NicheNormalizer.NormalizeTokens(query.Niches ?? []);
        var categorySearchTerms = CategorySearchTermParser.NormalizeAndValidate(query.CategorySearchTerms);
        AddTopicFitMode(filters, query.TopicFitMode, niches.Length > 0, categorySearchTerms is { Count: > 0 });
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.Niche, niches);
        AddCategorySearch(filters, ExportAnalyticsSnapshotSchema.Filters.Categories, "containsAny", categorySearchTerms);
        AddMultiSelect(filters, ExportAnalyticsSnapshotSchema.Filters.ExcludedNiche, NicheNormalizer.NormalizeTokens(query.ExcludedNiches ?? []));
        AddCategorySearch(
            filters,
            ExportAnalyticsSnapshotSchema.Filters.ExcludedCategories,
            "notContainsAny",
            query.ExcludedCategorySearchTerms);

        AddAvailability(filters, ExportAnalyticsServiceFilters.Casino.Field, query.CasinoAvailability);
        AddAvailability(filters, ExportAnalyticsServiceFilters.Crypto.Field, query.CryptoAvailability);
        AddAvailability(filters, ExportAnalyticsServiceFilters.LinkInsert.Field, query.LinkInsertAvailability);
        AddAvailability(filters, ExportAnalyticsServiceFilters.LinkInsertCasino.Field, query.LinkInsertCasinoAvailability);
        AddAvailability(filters, ExportAnalyticsServiceFilters.Dating.Field, query.DatingAvailability);
        AddQuarantine(filters, query.Quarantine);
        AddLastPublishedDate(filters, query.LastPublishedFrom, query.LastPublishedToExclusive);

        if (query.StopListDomains is { Count: > 0 })
        {
            filters.Add(new FilterSnapshotItemDto(
                Field: ExportAnalyticsSnapshotSchema.Filters.StopList,
                Kind: "boolean",
                Operator: "eq",
                Value: true));
        }

        return new FiltersSnapshotDto(CurrentSnapshotVersion, filters);
    }

    private static SortsSnapshotDto CreateSortSnapshot(SitesQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.SortBy))
        {
            return new SortsSnapshotDto(CurrentSnapshotVersion, []);
        }

        var direction = string.Equals(query.SortDir, SortingDefaults.Descending, StringComparison.OrdinalIgnoreCase)
            ? SortingDefaults.Descending
            : SortingDefaults.Ascending;

        return new SortsSnapshotDto(
            CurrentSnapshotVersion,
            [
                new SortSnapshotItemDto(
                    Field: ToBusinessFieldName(query.SortBy),
                    Direction: direction,
                    Priority: 1)
            ]);
    }

    private static string? CreateSearchSnapshotJson(SitesQuery query, ExportAnalyticsSearchContext? searchContext)
    {
        object? snapshot = searchContext?.Mode switch
        {
            ExportAnalyticsSnapshotSchema.Search.MultiSearchMode => new MultiSearchSnapshotDto(
                CurrentSnapshotVersion,
                searchContext.Mode,
                searchContext.InputCount.GetValueOrDefault(),
                searchContext.UniqueInputCount.GetValueOrDefault(),
                searchContext.FoundCount.GetValueOrDefault(),
                searchContext.NotFoundCount.GetValueOrDefault()),
            _ => CreateCatalogSearchSnapshot(query)
        };

        return snapshot is null ? null : JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static CatalogSearchSnapshotDto? CreateCatalogSearchSnapshot(SitesQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return null;
        }

        var normalizedQuery = DomainNormalizer.Normalize(query.Search);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        return new CatalogSearchSnapshotDto(
            CurrentSnapshotVersion,
            ExportAnalyticsSnapshotSchema.Search.CatalogSearchMode,
            query.Search.Trim(),
            normalizedQuery);
    }

    private static void AddNumberRange(
        List<FilterSnapshotItemDto> filters,
        string field,
        double? min,
        double? max)
    {
        AddNumberRange(
            filters,
            field,
            min.HasValue ? Convert.ToDecimal(min.Value, CultureInfo.InvariantCulture) : null,
            max.HasValue ? Convert.ToDecimal(max.Value, CultureInfo.InvariantCulture) : null);
    }

    private static void AddNumberRange(
        List<FilterSnapshotItemDto> filters,
        string field,
        long? min,
        long? max)
    {
        AddNumberRange(
            filters,
            field,
            min.HasValue ? (decimal?)min.Value : null,
            max.HasValue ? (decimal?)max.Value : null);
    }

    private static void AddNumberRange(
        List<FilterSnapshotItemDto> filters,
        string field,
        decimal? min,
        decimal? max)
    {
        if (!min.HasValue && !max.HasValue)
        {
            return;
        }

        filters.Add(new FilterSnapshotItemDto(
            Field: field,
            Kind: "numberRange",
            Operator: CreateRangeOperator(min.HasValue, max.HasValue),
            Value: new NumberRangeValueDto(min, max)));
    }

    private static void AddMultiSelect(
        List<FilterSnapshotItemDto> filters,
        string field,
        IEnumerable<string>? values)
    {
        var activeValues = values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

        if (activeValues.Length == 0)
        {
            return;
        }

        filters.Add(new FilterSnapshotItemDto(
            Field: field,
            Kind: "multiSelect",
            Operator: "anyOf",
            Value: activeValues));
    }

    private static void AddTopicFitMode(
        List<FilterSnapshotItemDto> filters,
        string? mode,
        bool hasNicheFilter,
        bool hasCategoryFilter)
    {
        if (!hasNicheFilter || !hasCategoryFilter)
        {
            return;
        }

        var normalizedMode = string.Equals(mode, TopicFitModeValues.Expand, StringComparison.OrdinalIgnoreCase)
            ? TopicFitModeValues.Expand
            : TopicFitModeValues.Narrow;

        filters.Add(new FilterSnapshotItemDto(
            Field: ExportAnalyticsSnapshotSchema.Filters.TopicFitMode,
            Kind: "enum",
            Operator: "eq",
            Value: normalizedMode));
    }

    private static void AddCategorySearch(
        List<FilterSnapshotItemDto> filters,
        string field,
        string filterOperator,
        IReadOnlyCollection<string?>? terms)
    {
        var activeTerms = CategorySearchTermParser.NormalizeAndValidate(terms);
        if (activeTerms is null || activeTerms.Count == 0)
        {
            return;
        }

        filters.Add(new FilterSnapshotItemDto(
            Field: field,
            Kind: "textSearch",
            Operator: filterOperator,
            Value: activeTerms));
    }

    private static void AddAvailability(
        List<FilterSnapshotItemDto> filters,
        string field,
        IReadOnlyCollection<ServiceAvailabilityStatus>? availability)
    {
        if (availability is null || availability.Count == 0)
        {
            return;
        }

        var values = availability
            .Distinct()
            .Select(FormatAvailability)
            .ToArray();

        filters.Add(new FilterSnapshotItemDto(
            Field: field,
            Kind: "availability",
            Operator: "in",
            Value: values));
    }

    private static void AddQuarantine(List<FilterSnapshotItemDto> filters, string? quarantine)
    {
        if (string.IsNullOrWhiteSpace(quarantine) ||
            string.Equals(quarantine, QuarantineFilterValues.All, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        filters.Add(new FilterSnapshotItemDto(
            Field: ExportAnalyticsSnapshotSchema.Filters.Quarantine,
            Kind: "enum",
            Operator: "eq",
            Value: quarantine.Trim().ToLowerInvariant()));
    }

    private static void AddLastPublishedDate(
        List<FilterSnapshotItemDto> filters,
        DateTime? from,
        DateTime? toExclusive)
    {
        if (!from.HasValue && !toExclusive.HasValue)
        {
            return;
        }

        object value;
        if (from is DateTime fromValue && toExclusive is DateTime toExclusiveValue)
        {
            value = new MonthRangeValueDto(
                MinMonth: FormatMonth(fromValue),
                MaxMonth: FormatMonth(toExclusiveValue.AddMonths(-1)));
        }
        else if (from is DateTime onlyFromValue)
        {
            value = new MonthValueDto(FormatMonth(onlyFromValue));
        }
        else
        {
            value = new MonthValueDto(FormatMonth(toExclusive!.Value));
        }

        filters.Add(new FilterSnapshotItemDto(
            Field: ExportAnalyticsSnapshotSchema.Filters.LastPublishedDate,
            Kind: "monthRange",
            Operator: CreateMonthRangeOperator(from.HasValue, toExclusive.HasValue),
            Value: value));
    }

    private static string CreateRangeOperator(bool hasMin, bool hasMax)
        => (hasMin, hasMax) switch
        {
            (true, true) => "between",
            (true, false) => "gte",
            _ => "lte"
        };

    private static string CreateMonthRangeOperator(bool hasFrom, bool hasToExclusive)
        => (hasFrom, hasToExclusive) switch
        {
            (true, true) => "between",
            (true, false) => "after",
            _ => "before"
        };

    private static string FormatMonth(DateTime value)
        => value.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static string FormatAvailability(ServiceAvailabilityStatus availability)
        => availability switch
        {
            ServiceAvailabilityStatus.Available => "available",
            ServiceAvailabilityStatus.NotAvailable => "notAvailable",
            ServiceAvailabilityStatus.AvailableWithUnknownPrice => "availableWithUnknownPrice",
            _ => "unknown"
        };

    private static string ToBusinessFieldName(string sortBy)
        => sortBy.Trim().ToLowerInvariant() switch
        {
            SortFields.Domain => ExportAnalyticsSnapshotSchema.Sort.Domain,
            SortFields.DR => ExportAnalyticsSnapshotSchema.Sort.Dr,
            SortFields.Traffic => ExportAnalyticsSnapshotSchema.Sort.Traffic,
            SortFields.Location => ExportAnalyticsSnapshotSchema.Sort.Location,
            SortFields.PriceUsd => ExportAnalyticsSnapshotSchema.Sort.PriceUsd,
            SortFields.PriceCasino => ExportAnalyticsSnapshotSchema.Sort.PriceCasino,
            SortFields.PriceCrypto => ExportAnalyticsSnapshotSchema.Sort.PriceCrypto,
            SortFields.PriceLinkInsert => ExportAnalyticsSnapshotSchema.Sort.PriceLinkInsert,
            SortFields.PriceLinkInsertCasino => ExportAnalyticsSnapshotSchema.Sort.PriceLinkInsertCasino,
            SortFields.PriceDating => ExportAnalyticsSnapshotSchema.Sort.PriceDating,
            SortFields.NumberDFLinks => ExportAnalyticsSnapshotSchema.Sort.NumberDfLinks,
            SortFields.Term => ExportAnalyticsSnapshotSchema.Sort.Term,
            SortFields.CreatedAt => ExportAnalyticsSnapshotSchema.Sort.CreatedAt,
            SortFields.UpdatedAt => ExportAnalyticsSnapshotSchema.Sort.UpdatedAt,
            SortFields.LastPublishedDate => ExportAnalyticsSnapshotSchema.Sort.LastPublishedDate,
            _ => SortingDefaults.DefaultSortBy
        };

    private sealed record FiltersSnapshotDto(int SchemaVersion, IReadOnlyList<FilterSnapshotItemDto> Filters);

    private sealed record FilterSnapshotItemDto(string Field, string Kind, string Operator, object Value);

    private sealed record NumberRangeValueDto(decimal? Min = null, decimal? Max = null);

    private sealed record MonthValueDto(string Month);

    private sealed record MonthRangeValueDto(string MinMonth, string MaxMonth);

    private sealed record SortsSnapshotDto(int SchemaVersion, IReadOnlyList<SortSnapshotItemDto> Sorts);

    private sealed record SortSnapshotItemDto(string Field, string Direction, int Priority);

    private sealed record CatalogSearchSnapshotDto(
        int SchemaVersion,
        string Mode,
        string Query,
        string NormalizedQuery);

    private sealed record MultiSearchSnapshotDto(
        int SchemaVersion,
        string Mode,
        int InputCount,
        int UniqueInputCount,
        int FoundCount,
        int NotFoundCount);
}
