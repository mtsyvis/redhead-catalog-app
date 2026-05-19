using Redhead.SitesCatalog.Api.Models.Sites;

namespace Redhead.SitesCatalog.Api.Models.Export;

/// <summary>
/// Request body for POST /api/sites/export/google-drive.
/// Normal exports use Filters.
/// Multi-search exports use SearchText plus optional Filters.
/// </summary>
public sealed class GoogleDriveExportRequest
{
    public SitesQueryRequest? Filters { get; set; }

    public string? SearchText { get; set; }
}
