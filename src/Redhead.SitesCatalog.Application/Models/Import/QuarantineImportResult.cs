namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of a quarantine import (CSV: Domain, Reason).
/// </summary>
public class QuarantineImportResult
{
    public int Matched { get; set; }
    public List<string> Unmatched { get; set; } = new();
    public int ErrorsCount { get; set; }
    public List<QuarantineImportError> Errors { get; set; } = new();
}

/// <summary>
/// Single validation/parsing error for quarantine import (row + message).
/// </summary>
public class QuarantineImportError
{
    public int RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
}
