using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for Last Published Date import from CSV (Domain, LastPublishedDate). Updates existing sites by exact normalized domain match.
/// </summary>
public interface ILastPublishedImportService
{
    /// <summary>
    /// Import last published dates from CSV. Headers: Domain (required), LastPublishedDate (required).
    /// Supported date formats: DD.MM.YYYY (day precision), or month+year (e.g. "January 2026", "Jan 2026", "01.2026") for month precision.
    /// Returns semantic summary counts, duplicate preview, and download handles.
    /// </summary>
    Task<SitesUpdateImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        CancellationToken cancellationToken = default);
}
