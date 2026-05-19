using Redhead.SitesCatalog.Api.Models.Sites;

namespace Redhead.SitesCatalog.Api.Models.Export;

/// <summary>
/// Request body for POST /api/export/sites-multi-search.xlsx
/// </summary>
public sealed class ExportMultiSearchRequest
{
    /// <summary>
    /// Domains/URLs separated by whitespace (max 500). Parsed and normalized for exact Domain match.
    /// </summary>
    public string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Current list filters (same semantics as Sites list). Search is taken from SearchText, not Filters.
    /// </summary>
    public SitesQueryRequest? Filters { get; set; }
}
