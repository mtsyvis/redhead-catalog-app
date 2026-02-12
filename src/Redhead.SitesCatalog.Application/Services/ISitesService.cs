using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for managing sites operations
/// </summary>
public interface ISitesService
{
    /// <summary>
    /// Get sites with filtering, pagination, and sorting
    /// </summary>
    Task<SitesListResult> GetSitesAsync(SitesQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get distinct location values
    /// </summary>
    Task<List<string>> GetLocationsAsync(CancellationToken cancellationToken = default);
}
