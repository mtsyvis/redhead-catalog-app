namespace Redhead.SitesCatalog.Application.Models.Import;

public sealed class WebmasterOffersImportResult
{
    public int ImportedCount { get; set; }
    public int SkippedDuplicateCount { get; set; }
    public int UnmatchedRowsCount { get; set; }
    public int InvalidRowsCount { get; set; }
    public int SavedWithWarningsCount { get; set; }
    public ImportDownloadsInfo? Downloads { get; set; }
}
