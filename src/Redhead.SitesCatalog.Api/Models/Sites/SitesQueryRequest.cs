using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Models.Sites;

/// <summary>
/// Query parameters for listing sites with filters, pagination, and sorting
/// </summary>
public class SitesQueryRequest
{
    // Pagination
    public int Page { get; set; } = PaginationDefaults.DefaultPage;
    public int PageSize { get; set; } = PaginationDefaults.DefaultPageSize;

    // Sorting
    /// <summary>
    /// Field to sort by. Valid values: domain, dr, traffic, location, priceusd, pricecasino, pricecrypto, pricelinkinsert, createdat, updatedat
    /// </summary>
    public string? SortBy { get; set; } = SortingDefaults.DefaultSortBy;
    
    /// <summary>
    /// Sort direction. Valid values: asc, desc
    /// </summary>
    public string? SortDir { get; set; } = SortingDefaults.DefaultSortDirection;

    // Search
    /// <summary>
    /// Search by domain (partial match). Automatically normalizes URLs (removes scheme, www, paths).
    /// Example: "https://www.example.com/path" matches "example.com"
    /// </summary>
    public string? Search { get; set; }

    // Range filters
    public int? DrMin { get; set; }
    public int? DrMax { get; set; }
    public long? TrafficMin { get; set; }
    public long? TrafficMax { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    // Location multi-select
    public List<string>? Locations { get; set; }

    // Allowed flags (null = ignore, true = must have value)
    public bool? CasinoAllowed { get; set; }
    public bool? CryptoAllowed { get; set; }
    public bool? LinkInsertAllowed { get; set; }

    /// <summary>
    /// Quarantine filter. Valid values: all (default - returns all sites), only (returns only quarantined sites), exclude (excludes quarantined sites)
    /// </summary>
    public string Quarantine { get; set; } = QuarantineFilterValues.All;
}
