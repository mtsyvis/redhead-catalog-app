namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of a Last Published Date import (CSV: Domain, LastPublishedDate).
/// </summary>
public class LastPublishedImportResult
{
    public int Matched { get; set; }
    public List<string> Unmatched { get; set; } = new();
    public int ErrorsCount { get; set; }
    public List<LastPublishedImportError> Errors { get; set; } = new();
}

/// <summary>
/// Single validation/parsing error for Last Published import (row + message).
/// </summary>
public class LastPublishedImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
