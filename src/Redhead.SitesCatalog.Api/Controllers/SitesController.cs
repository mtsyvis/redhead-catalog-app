using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Validation;
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
    /// Get sites with filtering, pagination, sorting, and body-only filters such as Stop list.
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<SitesListResponse>> SearchSites(
        [FromBody] SitesQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        var query = SitesMapper.ToQuery(request);

        var result = await _sitesService.GetSitesAsync(query, cancellationToken);
        var response = SitesMapper.ToResponse(result, includeInternalFields: CanViewInternalSiteFields());

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

        if (StopListParser.HasAnyInput(request.StopListDomains) ||
            StopListParser.HasAnyInput(request.Filters?.StopListDomains))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = StopListConstants.MultiSearchNotSupportedMessage
            });
        }

        var parseResult = MultiSearchParser.Parse(request.QueryText);

        var result = await _sitesService.MultiSearchSitesAsync(
            parseResult.UniqueDomains,
            parseResult.Duplicates,
            cancellationToken);

        var response = new MultiSearchResponse
        {
            Found = result.Found.Select(site => SitesMapper.ToSiteResponse(site, CanViewInternalSiteFields())).ToList(),
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
    /// Get filter options derived from current catalog data.
    /// </summary>
    [HttpGet("filter-options")]
    public async Task<ActionResult<FilterOptionsResponse>> GetFilterOptions(CancellationToken cancellationToken)
    {
        var niches = await _sitesService.GetNicheOptionsAsync(cancellationToken);
        var locations = await _sitesService.GetLocationFilterOptionsAsync(cancellationToken);

        return Ok(new FilterOptionsResponse
        {
            Niches = niches
                .Select(option => new FilterOptionResponse
                {
                    Value = option.Value,
                    Label = option.Label
                })
                .ToList(),
            Locations = new LocationFilterOptionsResponse
            {
                Groups = locations.Groups.Select(group => new LocationGroupFilterOptionResponse
                {
                    Key = group.Key,
                    DisplayName = group.DisplayName,
                    GroupType = group.GroupType,
                    LocationCount = group.LocationCount,
                    Locations = group.Locations.Select(location => new LocationFilterOptionResponse
                    {
                        Key = location.Key,
                        DisplayName = location.DisplayName
                    }).ToList()
                }).ToList(),
                Locations = locations.Locations.Select(location => new LocationFilterOptionResponse
                {
                    Key = location.Key,
                    DisplayName = location.DisplayName
                }).ToList(),
                Special = new LocationSpecialFilterOptionsResponse
                {
                    Unknown = new LocationFilterOptionResponse
                    {
                        Key = locations.Special.Unknown.Key,
                        DisplayName = locations.Special.Unknown.DisplayName
                    },
                    Other = locations.Special.Other is null
                        ? null
                        : new LocationFilterOptionResponse
                    {
                        Key = locations.Special.Other.Key,
                        DisplayName = locations.Special.Other.DisplayName
                    }
                }
            }
        });
    }

    /// <summary>
    /// Update site fields (Admin/SuperAdmin). Editable: DR, Traffic, Location, Language, prices, Niche, Categories, quarantine.
    /// Domain is read-only (route parameter). When IsQuarantined is false, reason is cleared.
    /// </summary>
    [HttpPut("{domain}")]
    [Authorize(Policy = AppPolicies.AdminAccess)]
    public async Task<ActionResult<SiteResponse>> UpdateSite(string domain, [FromBody] Models.Sites.UpdateSiteRequest request, CancellationToken cancellationToken)
    {
        var validationResult = SiteWriteValidator.ValidateAndNormalize(new SiteWriteInput
        {
            DR = request.DR,
            Traffic = request.Traffic,
            Location = request.Location,
            Language = request.Language,
            SponsoredTag = request.SponsoredTag,
            PriceUsd = request.PriceUsd,
            PriceCasino = request.PriceCasino,
            PriceCasinoStatus = request.PriceCasinoStatus,
            PriceCrypto = request.PriceCrypto,
            PriceCryptoStatus = request.PriceCryptoStatus,
            PriceLinkInsert = request.PriceLinkInsert,
            PriceLinkInsertStatus = request.PriceLinkInsertStatus,
            PriceLinkInsertCasino = request.PriceLinkInsertCasino,
            PriceLinkInsertCasinoStatus = request.PriceLinkInsertCasinoStatus,
            PriceDating = request.PriceDating,
            PriceDatingStatus = request.PriceDatingStatus,
            NumberDFLinks = request.NumberDFLinks,
            TermType = request.TermType,
            TermValue = request.TermValue,
            TermUnit = request.TermUnit,
            Niche = request.Niche,
            Categories = request.Categories,
            IsQuarantined = request.IsQuarantined,
            QuarantineReason = request.QuarantineReason
        });

        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                message = "Validation failed",
                fieldErrors = validationResult.FieldErrors
            });
        }

        var updated = await _sitesService.UpdateSiteAsync(
            domain,
            validationResult.NormalizedRequest!,
            HttpContext?.User?.FindFirstValue(ClaimTypes.Email),
            cancellationToken);

        if (updated == null)
        {
            return NotFound();
        }

        return Ok(SitesMapper.ToSiteResponse(updated));
    }

    private bool CanViewInternalSiteFields()
        => !User.IsInRole(AppRoles.Client);
}
