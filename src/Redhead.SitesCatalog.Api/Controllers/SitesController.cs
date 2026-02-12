using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Services;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SitesController : ControllerBase
{
    private readonly ISitesService _sitesService;

    public SitesController(ISitesService sitesService)
    {
        _sitesService = sitesService;
    }

    /// <summary>
    /// Get sites with filtering, pagination, and sorting
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SitesListResponse>> GetSites(
        [FromQuery] SitesQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = SitesMapper.ToQuery(request);
        var result = await _sitesService.GetSitesAsync(query, cancellationToken);
        var response = SitesMapper.ToResponse(result);

        return Ok(response);
    }

    /// <summary>
    /// Get distinct location values for filter dropdown
    /// </summary>
    [HttpGet("locations")]
    public async Task<ActionResult<LocationsResponse>> GetLocations(CancellationToken cancellationToken)
    {
        var locations = await _sitesService.GetLocationsAsync(cancellationToken);
        
        return Ok(new LocationsResponse
        {
            Locations = locations
        });
    }
}
