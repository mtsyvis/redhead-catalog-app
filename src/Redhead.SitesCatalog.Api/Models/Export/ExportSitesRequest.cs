using Redhead.SitesCatalog.Api.Models.Sites;

namespace Redhead.SitesCatalog.Api.Models.Export;

/// <summary>
/// Request body for POST /api/export/sites.xlsx.
/// </summary>
public sealed class ExportSitesRequest
{
    /// <summary>
    /// Current list filters, sorting, and search context.
    /// </summary>
    public SitesQueryRequest? Filters { get; set; }

    /// <summary>
    /// Current UI-visible Sites table column keys in display order.
    /// </summary>
    public List<string> VisibleColumnKeys { get; set; } = new();

    /// <summary>
    /// Excludes unavailable (quarantined) sites in addition to the active table filters.
    /// </summary>
    public bool ExcludeQuarantined { get; set; } = true;
}
