namespace Redhead.SitesCatalog.Application.Models.Import;

/// <summary>
/// Result of a sites add-only import
/// </summary>
public class SitesImportResult
{
    public int Inserted { get; set; }
    public int DuplicatesCount { get; set; }
    public List<string> Duplicates { get; set; } = new();
    public int ErrorsCount { get; set; }
    public List<SitesImportError> Errors { get; set; } = new();

    /// <summary>
    /// Creates a result for unsupported file type (single error, no rows processed).
    /// </summary>
    public static SitesImportResult UnsupportedFileType()
    {
        return new SitesImportResult
        {
            ErrorsCount = 1,
            Errors = new List<SitesImportError>
            {
                new() { RowNumber = 0, Message = "Unsupported file type. Use CSV." }
            }
        };
    }
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
