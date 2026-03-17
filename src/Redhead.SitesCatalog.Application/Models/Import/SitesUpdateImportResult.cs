namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Shared result model for update-style imports that update existing sites by Domain
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
    public string? Domain { get; set; }
    public string? Field { get; set; }
    public string? RawValue { get; set; }
}
