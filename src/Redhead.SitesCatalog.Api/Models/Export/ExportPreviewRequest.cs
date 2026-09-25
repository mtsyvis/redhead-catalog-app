using Redhead.SitesCatalog.Api.Models.Sites;

namespace Redhead.SitesCatalog.Api.Models.Export;

public sealed class ExportPreviewRequest
{
    public SitesQueryRequest? Filters { get; set; }
    public string? SearchText { get; set; }
    public bool ExcludeQuarantined { get; set; } = true;
}
