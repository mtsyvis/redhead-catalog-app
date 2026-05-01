using System.Globalization;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Api.Mappers;

/// <summary>
/// Mapper for converting between API models and application models for Sites
/// </summary>
public static class SitesMapper
{
    /// <summary>
    /// Maps API query request to application query model
    /// </summary>
    public static SitesQuery ToQuery(SitesQueryRequest request)
    {
        var (lastPublishedFrom, lastPublishedToExclusive) = ParseLastPublishedMonthRange(
            request.LastPublishedFromMonth,
            request.LastPublishedToMonth);

        return new SitesQuery
        {
            Page = request.Page,
            PageSize = request.PageSize,
            SortBy = request.SortBy ?? string.Empty,
            SortDir = request.SortDir ?? string.Empty,
            Search = request.Search,
            DrMin = request.DrMin,
            DrMax = request.DrMax,
            TrafficMin = request.TrafficMin,
            TrafficMax = request.TrafficMax,
            PriceMin = request.PriceMin,
            PriceMax = request.PriceMax,
            Locations = request.Locations,
            CasinoAllowed = request.CasinoAllowed,
            CryptoAllowed = request.CryptoAllowed,
            LinkInsertAllowed = request.LinkInsertAllowed,
            CasinoAvailability = ParseAvailabilityFilter(request.CasinoAvailability),
            CryptoAvailability = ParseAvailabilityFilter(request.CryptoAvailability),
            LinkInsertAvailability = ParseAvailabilityFilter(request.LinkInsertAvailability),
            LinkInsertCasinoAvailability = ParseAvailabilityFilter(request.LinkInsertCasinoAvailability),
            DatingAvailability = ParseAvailabilityFilter(request.DatingAvailability),
            Quarantine = request.Quarantine,
            LastPublishedFrom = lastPublishedFrom,
            LastPublishedToExclusive = lastPublishedToExclusive
        };
    }

    /// <summary>
    /// Maps application result to API response
    /// </summary>
    public static SitesListResponse ToResponse(SitesListResult result)
    {
        return new SitesListResponse
        {
            Items = result.Items.Select(ToSiteResponse).ToList(),
            Total = result.Total
        };
    }

    /// <summary>
    /// Maps application site DTO to API site response
    /// </summary>
    public static SiteResponse ToSiteResponse(SiteDto dto)
    {
        return new SiteResponse
        {
            Domain = dto.Domain,
            DR = dto.DR,
            Traffic = dto.Traffic,
            Location = dto.Location,
            SponsoredTag = dto.SponsoredTag,
            PriceUsd = dto.PriceUsd,
            PriceCasino = dto.PriceCasino,
            PriceCasinoStatus = dto.PriceCasinoStatus,
            PriceCrypto = dto.PriceCrypto,
            PriceCryptoStatus = dto.PriceCryptoStatus,
            PriceLinkInsert = dto.PriceLinkInsert,
            PriceLinkInsertStatus = dto.PriceLinkInsertStatus,
            PriceLinkInsertCasino = dto.PriceLinkInsertCasino,
            PriceLinkInsertCasinoStatus = dto.PriceLinkInsertCasinoStatus,
            PriceDating = dto.PriceDating,
            PriceDatingStatus = dto.PriceDatingStatus,
            NumberDFLinks = dto.NumberDFLinks,
            TermType = dto.TermType,
            TermValue = dto.TermValue,
            TermUnit = dto.TermUnit,
            Niche = dto.Niche,
            Categories = dto.Categories,
            IsQuarantined = dto.IsQuarantined,
            QuarantineReason = dto.QuarantineReason,
            QuarantineUpdatedAtUtc = dto.QuarantineUpdatedAtUtc,
            CreatedAtUtc = dto.CreatedAtUtc,
            UpdatedAtUtc = dto.UpdatedAtUtc,
            LastPublishedDate = dto.LastPublishedDate,
            LastPublishedDateIsMonthOnly = dto.LastPublishedDateIsMonthOnly
        };
    }

    private static ServiceAvailabilityFilter? ParseAvailabilityFilter(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = rawValue.Trim().ToLowerInvariant();
        return normalized switch
        {
            "all" => ServiceAvailabilityFilter.All,
            "available" => ServiceAvailabilityFilter.Available,
            "notavailable" => ServiceAvailabilityFilter.NotAvailable,
            "unknown" => ServiceAvailabilityFilter.Unknown,
            _ => throw new RequestValidationException(
                $"Invalid availability filter value '{rawValue}'. Allowed values: all, available, notAvailable, unknown.")
        };
    }

    private static (DateTime? from, DateTime? toExclusive) ParseLastPublishedMonthRange(
        string? fromMonth, string? toMonth)
    {
        DateTime? from = null;
        DateTime? toExclusive = null;

        if (!string.IsNullOrWhiteSpace(fromMonth))
        {
            from = ParseYearMonth(fromMonth);
        }

        if (!string.IsNullOrWhiteSpace(toMonth))
        {
            var toFirstDay = ParseYearMonth(toMonth);
            toExclusive = toFirstDay.AddMonths(1);
        }

        if (from.HasValue && toExclusive.HasValue && from.Value >= toExclusive.Value)
        {
            throw new RequestValidationException(
                $"LastPublishedFromMonth '{fromMonth}' must not be later than LastPublishedToMonth '{toMonth}'.");
        }

        return (from, toExclusive);
    }

    private static DateTime ParseYearMonth(string value)
    {
        if (!DateTime.TryParseExact(
                value.Trim(),
                "yyyy-MM",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            throw new RequestValidationException(
                $"Invalid month format '{value}'. Expected format: yyyy-MM (e.g. 2025-01).");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }
}
