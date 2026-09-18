using System.Globalization;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Validation;

public static class AnalyticsRequestMapper
{
    public static AnalyticsQueryMapping<BusinessDemandAnalyticsQuery> ToBusinessDemandQuery(
        BusinessDemandAnalyticsRequest request,
        DateTimeOffset nowUtc)
    {
        var common = MapCommonFilters(
            request.From,
            request.To,
            request.ClientId,
            request.Destination,
            request.Status,
            nowUtc);
        if (common.Error != null)
        {
            return AnalyticsQueryMapping<BusinessDemandAnalyticsQuery>.Invalid(common.Error);
        }

        var filters = common.Query!;
        return AnalyticsQueryMapping<BusinessDemandAnalyticsQuery>.Valid(new BusinessDemandAnalyticsQuery
        {
            FromUtc = filters.FromUtc,
            ToUtc = filters.ToUtc,
            ClientId = filters.ClientId,
            Destination = filters.Destination,
            Status = filters.Status
        });
    }

    public static AnalyticsQueryMapping<ExportActivityAnalyticsQuery> ToExportActivityQuery(
        ExportActivityAnalyticsRequest request,
        DateTimeOffset nowUtc)
    {
        var common = MapCommonFilters(
            request.From,
            request.To,
            request.ClientId,
            request.Destination,
            request.Status,
            nowUtc);
        if (common.Error != null)
        {
            return AnalyticsQueryMapping<ExportActivityAnalyticsQuery>.Invalid(common.Error);
        }

        var paginationError = ValidatePagination(request.Page, request.PageSize);
        if (paginationError != null)
        {
            return AnalyticsQueryMapping<ExportActivityAnalyticsQuery>.Invalid(paginationError);
        }

        var filters = common.Query!;
        return AnalyticsQueryMapping<ExportActivityAnalyticsQuery>.Valid(new ExportActivityAnalyticsQuery
        {
            FromUtc = filters.FromUtc,
            ToUtc = filters.ToUtc,
            NowUtc = nowUtc.UtcDateTime,
            ClientId = filters.ClientId,
            Destination = filters.Destination,
            Status = filters.Status,
            RecentExportsPage = request.Page,
            RecentExportsPageSize = request.PageSize
        });
    }

    public static AnalyticsQueryMapping<MissingDomainsAnalyticsQuery> ToMissingDomainsQuery(
        MissingDomainsAnalyticsRequest request,
        DateTimeOffset nowUtc)
    {
        var to = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        var from = to.AddDays(-29);
        if (request.AllTime && (!string.IsNullOrWhiteSpace(request.From) || !string.IsNullOrWhiteSpace(request.To)))
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "All time cannot be combined with from/to dates.");
        }

        if ((!string.IsNullOrWhiteSpace(request.From) && !TryParseDate(request.From, out from)) ||
            (!string.IsNullOrWhiteSpace(request.To) && !TryParseDate(request.To, out to)))
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "Invalid date. Expected format: yyyy-MM-dd.");
        }

        if (from > to || to == DateOnly.MaxValue)
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "Choose a valid date range with From earlier than or equal to To.");
        }

        var paginationError = ValidatePagination(request.Page, request.PageSize);
        if (paginationError != null)
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(paginationError);
        }

        var role = request.Role?.Trim().ToLowerInvariant();
        if (role is not (null or "" or "all" or "client" or "lite"))
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "Invalid role. Allowed values: Client, Lite.");
        }

        var status = request.CatalogStatus?.Trim().ToLowerInvariant();
        if (status is not (null or "" or "all" or "missing" or "added"))
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "Invalid catalog status. Allowed values: missing, added.");
        }

        if (request.Domain?.Length > 253)
        {
            return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Invalid(
                "Domain search must not exceed 253 characters.");
        }

        return AnalyticsQueryMapping<MissingDomainsAnalyticsQuery>.Valid(new MissingDomainsAnalyticsQuery
        {
            FromUtc = request.AllTime ? null : from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ToUtc = request.AllTime ? null : to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Role = role == "client" ? AppRoles.Client : role == "lite" ? AppRoles.Lite : null,
            Domain = DomainNormalizer.Normalize(request.Domain),
            IsInCatalog = status == "added" ? true : status == "missing" ? false : null,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private static string? ValidatePagination(int page, int pageSize)
    {
        if (page < PaginationDefaults.DefaultPage)
        {
            return $"Page must be greater than or equal to {PaginationDefaults.DefaultPage}.";
        }

        if (!PaginationDefaults.AnalyticsPageSizes.Contains(pageSize))
        {
            return $"Invalid pageSize. Allowed values: {string.Join(", ", PaginationDefaults.AnalyticsPageSizes)}.";
        }

        var offset = ((long)page - PaginationDefaults.DefaultPage) * pageSize;
        if (offset > int.MaxValue)
        {
            return "Page is too large for the selected pageSize.";
        }

        return null;
    }

    private static AnalyticsQueryMapping<AnalyticsCommonFilters> MapCommonFilters(
        string? from,
        string? to,
        string? clientId,
        string? destination,
        string? status,
        DateTimeOffset nowUtc)
    {
        var todayUtc = DateOnly.FromDateTime(nowUtc.UtcDateTime);
        var fromDate = todayUtc.AddDays(-29);
        var toDate = todayUtc;

        if (!string.IsNullOrWhiteSpace(from) &&
            !TryParseDate(from, out fromDate))
        {
            return AnalyticsQueryMapping<AnalyticsCommonFilters>.Invalid(
                "Invalid from date. Expected format: yyyy-MM-dd.");
        }

        if (!string.IsNullOrWhiteSpace(to) &&
            !TryParseDate(to, out toDate))
        {
            return AnalyticsQueryMapping<AnalyticsCommonFilters>.Invalid(
                "Invalid to date. Expected format: yyyy-MM-dd.");
        }

        if (fromDate > toDate)
        {
            return AnalyticsQueryMapping<AnalyticsCommonFilters>.Invalid(
                "From date must be earlier than or equal to to date.");
        }

        var normalizedDestination = NormalizeDestination(destination);
        if (normalizedDestination.IsInvalid)
        {
            return AnalyticsQueryMapping<AnalyticsCommonFilters>.Invalid(
                "Invalid destination. Allowed values: Download, GoogleDrive.");
        }

        var normalizedStatus = NormalizeStatus(status);
        if (normalizedStatus.IsInvalid)
        {
            return AnalyticsQueryMapping<AnalyticsCommonFilters>.Invalid(
                "Invalid status. Allowed values: successful, partial, blocked.");
        }

        return AnalyticsQueryMapping<AnalyticsCommonFilters>.Valid(new AnalyticsCommonFilters(
            FromUtc: fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ToUtc: toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ClientId: NormalizeOptionalText(clientId),
            Destination: normalizedDestination.Value,
            Status: normalizedStatus.Value));
    }

    private static bool TryParseDate(string value, out DateOnly date)
        => DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static NormalizedFilterValue NormalizeDestination(string? rawValue)
    {
        var value = NormalizeOptionalText(rawValue);
        if (value == null || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizedFilterValue.Valid(null);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "download" => NormalizedFilterValue.Valid(ExportConstants.DestinationDownload),
            "googledrive" or "google-drive" or "google drive" =>
                NormalizedFilterValue.Valid(ExportConstants.DestinationGoogleDrive),
            _ => NormalizedFilterValue.Invalid()
        };
    }

    private static NormalizedFilterValue NormalizeStatus(string? rawValue)
    {
        var value = NormalizeOptionalText(rawValue);
        if (value == null || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizedFilterValue.Valid(null);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            AnalyticsStatusFilters.Successful =>
                NormalizedFilterValue.Valid(AnalyticsStatusFilters.Successful),
            AnalyticsStatusFilters.Partial =>
                NormalizedFilterValue.Valid(AnalyticsStatusFilters.Partial),
            AnalyticsStatusFilters.Blocked =>
                NormalizedFilterValue.Valid(AnalyticsStatusFilters.Blocked),
            _ => NormalizedFilterValue.Invalid()
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record AnalyticsCommonFilters(
        DateTime FromUtc,
        DateTime ToUtc,
        string? ClientId,
        string? Destination,
        string? Status);

    private sealed record NormalizedFilterValue(string? Value, bool IsInvalid)
    {
        public static NormalizedFilterValue Valid(string? value) => new(value, false);

        public static NormalizedFilterValue Invalid() => new(null, true);
    }
}

public sealed record AnalyticsQueryMapping<TQuery>(TQuery? Query, string? Error)
{
    public static AnalyticsQueryMapping<TQuery> Valid(TQuery query)
        => new(query, null);

    public static AnalyticsQueryMapping<TQuery> Invalid(string error)
        => new(default, error);
}
