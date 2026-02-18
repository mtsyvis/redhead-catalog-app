using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
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
    /// Multi-search by domains/URLs: exact match on normalized Domain. Max 500 inputs.
    /// </summary>
    [HttpPost("multi-search")]
    public async Task<ActionResult<MultiSearchResponse>> MultiSearch(
        [FromBody] MultiSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(ProblemDetailsValidation("Request body is required."));
        }

        MultiSearchParseResult parseResult;
        try
        {
            parseResult = MultiSearchParser.Parse(request.QueryText);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ProblemDetailsValidation(ex.Message));
        }

        var result = await _sitesService.MultiSearchSitesAsync(
            parseResult.UniqueDomains,
            parseResult.Duplicates,
            cancellationToken);

        var response = new MultiSearchResponse
        {
            Found = result.Found.Select(SitesMapper.ToSiteResponse).ToList(),
            NotFound = result.NotFound,
            Duplicates = result.Duplicates
        };

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

    private static Microsoft.AspNetCore.Mvc.ProblemDetails ProblemDetailsValidation(string detail)
    {
        return new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "Validation Error",
            Status = StatusCodes.Status400BadRequest,
            Detail = detail
        };
    }
}
