using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing export operations
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export sites as CSV with effective policy enforcement (role + per-user override).
    /// </summary>
    Task<ExportResult> ExportSitesAsCsvAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export multi-search as CSV: filtered Found (effective policy limit) + all Not found domains.
    /// query must have Search = null (domain list comes from queryText).
    /// </summary>
    Task<ExportResult> ExportMultiSearchAsCsvAsync(
        string queryText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default);
}
