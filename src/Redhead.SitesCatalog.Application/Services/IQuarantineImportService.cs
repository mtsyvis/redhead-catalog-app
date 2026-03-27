using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for quarantine import from CSV (Domain, Reason). Updates existing sites by exact normalized domain match.
/// </summary>
public interface IQuarantineImportService
{
    /// <summary>
    /// Import quarantine from CSV. Headers: Domain (required), Reason (optional).
    /// Matched sites: IsQuarantined=true, QuarantineReason=trim(Reason), QuarantineUpdatedAtUtc=now.
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
