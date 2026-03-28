using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

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
            return BadRequest("Request body is required.");
        }

        var parseResult = MultiSearchParser.Parse(request.QueryText);

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

    /// <summary>
    /// Update site fields (Admin/SuperAdmin). Editable: DR, Traffic, Location, prices, Niche, Categories, quarantine.
    /// Domain is read-only (route parameter). When IsQuarantined is false, reason is cleared.
    /// </summary>
    [HttpPut("{domain}")]
    [Authorize(Policy = AppPolicies.AdminAccess)]
    public async Task<ActionResult<SiteResponse>> UpdateSite(string domain, [FromBody] Models.Sites.UpdateSiteRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = UpdateSiteRequestValidator.Validate(request);
        if (validationErrors != null)
        {
            return BadRequest(new
            {
                message = "Validation failed",
                fieldErrors = validationErrors
            });
        }

        var appRequest = new Redhead.SitesCatalog.Application.Models.UpdateSiteRequest
        {
            DR = request.DR!.Value,
            Traffic = request.Traffic!.Value,
            Location = request.Location!.Trim(),
            LinkType = string.IsNullOrWhiteSpace(request.LinkType) ? null : request.LinkType.Trim(),
            SponsoredTag = string.IsNullOrWhiteSpace(request.SponsoredTag) ? null : request.SponsoredTag.Trim(),
            PriceUsd = request.PriceUsd!.Value,
            PriceCasino = request.PriceCasino,
            PriceCasinoStatus = UpdateSiteRequestValidator.ResolveStatusOrInfer(request.PriceCasino, request.PriceCasinoStatus),
            PriceCrypto = request.PriceCrypto,
            PriceCryptoStatus = UpdateSiteRequestValidator.ResolveStatusOrInfer(request.PriceCrypto, request.PriceCryptoStatus),
            PriceLinkInsert = request.PriceLinkInsert,
            PriceLinkInsertStatus = UpdateSiteRequestValidator.ResolveStatusOrInfer(request.PriceLinkInsert, request.PriceLinkInsertStatus),
            Niche = string.IsNullOrWhiteSpace(request.Niche) ? null : request.Niche.Trim(),
            Categories = string.IsNullOrWhiteSpace(request.Categories) ? null : request.Categories.Trim(),
            IsQuarantined = request.IsQuarantined,
            QuarantineReason = string.IsNullOrWhiteSpace(request.QuarantineReason) ? null : request.QuarantineReason.Trim()
        };

        var updated = await _sitesService.UpdateSiteAsync(domain, appRequest, cancellationToken);

        if (updated == null)
        {
            return NotFound();
        }

        return Ok(SitesMapper.ToSiteResponse(updated));
    }
}
