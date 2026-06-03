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
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export multi-search as Excel: filtered Found rows (effective policy limit) plus Not found domains.
    /// Domain/default sort preserves normalized input order; other sorts apply to Found rows.
    /// query must have Search = null (domain list comes from searchText).
    /// </summary>
    Task<ExportResult> ExportMultiSearchAsExcelAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        IReadOnlyList<string> visibleColumnKeys,
        CancellationToken cancellationToken = default);
}
