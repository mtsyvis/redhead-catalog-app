using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Service for building IQueryable for sites with filters and sorting
/// </summary>
public interface ISitesQueryBuilder
{
    /// <summary>
    /// Build a filtered and sorted IQueryable from the provided query parameters
    /// </summary>
    /// <param name="baseQuery">Base IQueryable to apply filters to</param>
    /// <param name="query">Query parameters with filters, sorting</param>
    /// <returns>Filtered and sorted IQueryable</returns>
    IQueryable<Site> BuildQuery(IQueryable<Site> baseQuery, SitesQuery query);
}
