using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Query model for sites listing
/// </summary>
public class SitesQuery
{
    // Pagination
    public int Page { get; set; }
    public int PageSize { get; set; }

    // Sorting
    public string SortBy { get; set; } = string.Empty;
    public string SortDir { get; set; } = string.Empty;

    // Search
    public string? Search { get; set; }

    // Stop list exclusion (normalized exact domains)
    public List<string>? StopListDomains { get; set; }

    // Range filters
    public double? DrMin { get; set; }
    public double? DrMax { get; set; }
    public long? TrafficMin { get; set; }
    public long? TrafficMax { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }

    // Legacy location multi-select. Prefer LocationKeys for canonical location filtering.
    public List<string>? Locations { get; set; }

    // Canonical location filtering
    public List<string>? LocationKeys { get; set; }
    public List<string>? LocationGroupKeys { get; set; }
    public bool IncludeUnknownLocation { get; set; }
    public bool IncludeOtherLocation { get; set; }

    // Language multi-select
    public List<string>? Languages { get; set; }

    // Niche multi-select
    public List<string>? Niches { get; set; }

    // Categories substring search terms
    public List<string>? CategorySearchTerms { get; set; }

    public ServiceAvailabilityFilter? CasinoAvailability { get; set; }
    public ServiceAvailabilityFilter? CryptoAvailability { get; set; }
    public ServiceAvailabilityFilter? LinkInsertAvailability { get; set; }
    public ServiceAvailabilityFilter? LinkInsertCasinoAvailability { get; set; }
    public ServiceAvailabilityFilter? DatingAvailability { get; set; }

    // Quarantine filter
    public string Quarantine { get; set; } = string.Empty;

    // LastPublishedDate range filter (parsed from yyyy-MM month inputs)
    public DateTime? LastPublishedFrom { get; set; }
    public DateTime? LastPublishedToExclusive { get; set; }
}
