using System.Text.Json.Serialization;
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
    /// Field to sort by. Valid values: domain, dr, traffic, location, priceusd, pricecasino, pricecrypto, pricelinkinsert, createdat, updatedat, lastpublisheddate
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
    public double? DrMin { get; set; }
    public double? DrMax { get; set; }
    public long? TrafficMin { get; set; }
    public long? TrafficMax { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    // Location multi-select (frontend sends "location")
    [JsonPropertyName("location")]
    public List<string>? Locations { get; set; }

    // Allowed flags (null = ignore, true = must have value)
    public bool? CasinoAllowed { get; set; }
    public bool? CryptoAllowed { get; set; }
    public bool? LinkInsertAllowed { get; set; }

    /// <summary>
    /// Optional availability filter for PriceCasino status.
    /// Allowed values: all, available, notAvailable, unknown.
    /// If set, takes precedence over CasinoAllowed.
    /// </summary>
    public string? CasinoAvailability { get; set; }

    /// <summary>
    /// Optional availability filter for PriceCrypto status.
    /// Allowed values: all, available, notAvailable, unknown.
    /// If set, takes precedence over CryptoAllowed.
    /// </summary>
    public string? CryptoAvailability { get; set; }

    /// <summary>
    /// Optional availability filter for PriceLinkInsert status.
    /// Allowed values: all, available, notAvailable, unknown.
    /// If set, takes precedence over LinkInsertAllowed.
    /// </summary>
    public string? LinkInsertAvailability { get; set; }

    /// <summary>
    /// Optional availability filter for PriceLinkInsertCasino status.
    /// Allowed values: all, available, notAvailable, unknown.
    /// </summary>
    public string? LinkInsertCasinoAvailability { get; set; }

    /// <summary>
    /// Optional availability filter for PriceDating status.
    /// Allowed values: all, available, notAvailable, unknown.
    /// </summary>
    public string? DatingAvailability { get; set; }

    /// <summary>
    /// Quarantine filter. Valid values: all (default - returns all sites), only (returns only quarantined sites), exclude (excludes quarantined sites)
    /// </summary>
    public string Quarantine { get; set; } = QuarantineFilterValues.All;

    /// <summary>
    /// Inclusive lower bound for LastPublishedDate filter. Format: yyyy-MM (e.g. 2025-01).
    /// Matches from the first day of the specified month.
    /// </summary>
    public string? LastPublishedFromMonth { get; set; }

    /// <summary>
    /// Inclusive upper bound for LastPublishedDate filter. Format: yyyy-MM (e.g. 2025-12).
    /// Matches through the last day of the specified month (exclusive upper bound is first day of the next month).
    /// </summary>
    public string? LastPublishedToMonth { get; set; }
}
