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

    /// <summary>
    /// Multi-search: exact match by normalized domains. Single DB query.
    /// </summary>
    /// <param name="normalizedDomains">Unique normalized domains to look up</param>
    /// <param name="duplicates">Domains that appeared more than once in input (for response)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<MultiSearchSitesResult> MultiSearchSitesAsync(
        IReadOnlyList<string> normalizedDomains,
        IReadOnlyList<string> duplicates,
        CancellationToken cancellationToken = default);
}
