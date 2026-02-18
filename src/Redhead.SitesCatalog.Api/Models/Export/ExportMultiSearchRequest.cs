using Redhead.SitesCatalog.Api.Models.Sites;

namespace Redhead.SitesCatalog.Api.Models.Export;

/// <summary>
/// Request body for POST /api/export/sites-multi-search.csv
/// </summary>
public class ExportMultiSearchRequest
{
    /// <summary>
    /// Domains/URLs separated by whitespace (max 500). Parsed and normalized for exact Domain match.
    /// </summary>
    public string QueryText { get; set; } = string.Empty;

    /// <summary>
    /// Current list filters (same semantics as Sites list). Search is taken from QueryText, not Filters.
    /// </summary>
    public SitesQueryRequest? Filters { get; set; }

    /// <summary>
    /// Sort field (e.g. domain, dr, traffic).
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort direction: asc or desc.
    /// </summary>
    public string? SortDir { get; set; }
}
