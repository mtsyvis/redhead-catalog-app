using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for sites add-only import from CSV/XLSX
/// </summary>
public interface ISitesImportService
{
    /// <summary>
    /// Import sites from file (add-only). Duplicates are skipped; errors collected.
    /// </summary>
    Task<SitesImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default);
}
