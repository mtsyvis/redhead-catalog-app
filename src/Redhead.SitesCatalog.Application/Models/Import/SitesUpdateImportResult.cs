namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of a sites update import (mass update of existing sites by Domain).
/// </summary>
public class SitesUpdateImportResult
{
    public int Matched { get; set; }
    public List<string> Unmatched { get; set; } = new();
    public int ErrorsCount { get; set; }
    public List<SitesUpdateImportError> Errors { get; set; } = new();
    public int DuplicatesCount { get; set; }
    public List<string> Duplicates { get; set; } = new();
}

/// <summary>
/// Single validation/parsing error for sites update import (row + message).
/// </summary>
public class SitesUpdateImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
