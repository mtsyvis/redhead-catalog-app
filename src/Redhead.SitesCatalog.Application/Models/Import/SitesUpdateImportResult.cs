namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Shared result model for update-style imports that update existing sites by Domain
/// </summary>
public class SitesUpdateImportResult
{
    public int UpdatedCount { get; set; }
    public int UnmatchedRowsCount { get; set; }
    public int DuplicateDomainsCount { get; set; }
    public List<string> DuplicateDomainsPreview { get; set; } = new();
    public int InvalidRowsCount { get; set; }
    public ImportDownloadsInfo? Downloads { get; set; }
}
