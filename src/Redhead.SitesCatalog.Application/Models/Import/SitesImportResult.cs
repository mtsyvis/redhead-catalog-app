namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of a sites add-only import
/// </summary>
public class SitesImportResult
{
    public int InsertedCount { get; set; }
    public int SkippedExistingCount { get; set; }
    public int DuplicateDomainsCount { get; set; }
    public List<string> DuplicateDomainsPreview { get; set; } = new();
    public int InvalidRowsCount { get; set; }
    public ImportDownloadsInfo? Downloads { get; set; }
}

/// <summary>
/// Single parsing/validation error with row number
/// </summary>
public class SitesImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Field { get; set; }
    public string? RawValue { get; set; }
}
