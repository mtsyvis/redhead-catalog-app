using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;

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
            Quarantine = request.Quarantine
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
            PriceUsd = dto.PriceUsd,
            PriceCasino = dto.PriceCasino,
            PriceCrypto = dto.PriceCrypto,
            PriceLinkInsert = dto.PriceLinkInsert,
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
}
