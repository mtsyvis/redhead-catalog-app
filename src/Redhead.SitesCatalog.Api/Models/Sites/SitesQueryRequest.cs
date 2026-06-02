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
    /// Field to sort by. Valid values: domain, dr, traffic, location, priceusd, pricecasino, pricecrypto, pricelinkinsert, pricelinkinsertcasino, pricedating, numberdflinks, term, createdat, updatedat, lastpublisheddate
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

    /// <summary>
    /// Domains/URLs to exclude from normal search/filter results. Normalized and matched by exact Domain.
    /// Send in the POST request body for /api/sites/search or inside the filters object for export endpoints.
    /// </summary>
    public List<string>? StopListDomains { get; set; }

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

    public List<string>? LocationKeys { get; set; }
    public List<string>? LocationGroupKeys { get; set; }
    public List<string>? ExcludedLocationKeys { get; set; }
    public bool IncludeUnknownLocation { get; set; }
    public bool IncludeOtherLocation { get; set; }

    // Language multi-select
    public List<string>? Languages { get; set; }

    // Niche multi-select
    public List<string>? Niches { get; set; }

    /// <summary>
    /// Category keywords or phrases. Each term is matched as a literal case-insensitive substring in Categories.
    /// Multiple terms use OR semantics and are combined with other filters using AND.
    /// </summary>
    public List<string?>? CategorySearchTerms { get; set; }

    /// <summary>
    /// Controls how Niche and Categories include filters combine when both are active.
    /// Allowed values: expand (Niche OR Categories), narrow (Niche AND Categories).
    /// </summary>
    public string? TopicFitMode { get; set; } = TopicFitModeValues.Narrow;

    // Niche multi-select exclusion
    public List<string>? ExcludedNiches { get; set; }

    /// <summary>
    /// Category keywords or phrases to exclude. Each term is matched as a literal case-insensitive substring in Categories.
    /// </summary>
    public List<string?>? ExcludedCategorySearchTerms { get; set; }

    /// <summary>
    /// Optional availability filters for PriceCasino status.
    /// Allowed values: unknown, available, notAvailable, availableWithUnknownPrice.
    /// </summary>
    public List<string>? CasinoAvailability { get; set; }

    /// <summary>
    /// Optional availability filters for PriceCrypto status.
    /// Allowed values: unknown, available, notAvailable, availableWithUnknownPrice.
    /// </summary>
    public List<string>? CryptoAvailability { get; set; }

    /// <summary>
    /// Optional availability filters for PriceLinkInsert status.
    /// Allowed values: unknown, available, notAvailable, availableWithUnknownPrice.
    /// </summary>
    public List<string>? LinkInsertAvailability { get; set; }

    /// <summary>
    /// Optional availability filters for PriceLinkInsertCasino status.
    /// Allowed values: unknown, available, notAvailable, availableWithUnknownPrice.
    /// </summary>
    public List<string>? LinkInsertCasinoAvailability { get; set; }

    /// <summary>
    /// Optional availability filters for PriceDating status.
    /// Allowed values: unknown, available, notAvailable, availableWithUnknownPrice.
    /// </summary>
    public List<string>? DatingAvailability { get; set; }

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
