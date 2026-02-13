using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing export operations
/// </summary>
public interface IExportService
{
    /// <summary>
    /// Export sites as CSV with role-based limit enforcement
    /// </summary>
    /// <param name="query">Query parameters for filtering and sorting</param>
    /// <param name="userId">User ID for audit logging</param>
    /// <param name="userEmail">User email for audit logging</param>
    /// <param name="userRole">User role for limit enforcement</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CSV stream</returns>
    Task<Stream> ExportSitesAsCsvAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken = default);
}
