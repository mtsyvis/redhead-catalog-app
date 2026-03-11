using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for mass-update import of existing sites from CSV (same columns as sites import).
/// Domain is lookup key only; other columns are updated. No inserts.
/// </summary>
public interface ISitesUpdateImportService
{
    /// <summary>
    /// Import CSV to update existing sites by normalized domain. Returns matched, unmatched, errors, and duplicates.
    /// </summary>
    Task<SitesUpdateImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default);
}
