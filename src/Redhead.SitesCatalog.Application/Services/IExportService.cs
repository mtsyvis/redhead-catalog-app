using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing export operations
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export sites as Excel with effective policy enforcement (role + per-user override).
    /// </summary>
    Task<ExportResult> ExportSitesAsExcelAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export multi-search as Excel: filtered Found (effective policy limit) + matching Not found domains.
    /// query must have Search = null (domain list comes from queryText).
    /// </summary>
    Task<ExportResult> ExportMultiSearchAsExcelAsync(
        string queryText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default);
}
