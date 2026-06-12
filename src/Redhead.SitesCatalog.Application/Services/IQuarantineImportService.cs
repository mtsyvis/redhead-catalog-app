using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for availability import from CSV. Updates existing sites by exact normalized domain match.
/// </summary>
public interface IQuarantineImportService
{
    /// <summary>
    /// Import site availability from CSV using the selected action.
    /// MarkUnavailable headers: Domain, Reason. RestoreAvailable headers: Domain.
    /// Returns semantic summary counts, duplicate preview, and download handles.
    /// </summary>
    Task<SitesUpdateImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        string? contentType,
        string userId,
        string userEmail,
        SiteAvailabilityImportAction action,
        CancellationToken cancellationToken = default);
}
