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
    private readonly ILiteMultiSearchUsageService _liteMultiSearchUsageService;

    public SitesController(
        ISitesService sitesService,
        ILiteMultiSearchUsageService liteMultiSearchUsageService)
    {
        _sitesService = sitesService;
        _liteMultiSearchUsageService = liteMultiSearchUsageService;
    }

    /// <summary>
    /// Get sites with filtering, pagination, sorting, and body-only filters such as Stop list.
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<SitesListResponse>> SearchSites(
        [FromBody] SitesQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

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
    /// Multi-search by domains/URLs: exact match on normalized Domain. Max 5000 inputs.
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
        if (IsLiteUser())
        {
            var userId = HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var usage = await _liteMultiSearchUsageService.TryConsumeAsync(
                userId,
                parseResult.UniqueDomains.Count,
                cancellationToken);
            if (!usage.Allowed)
            {
                return LiteMultiSearchUsageProblem(usage);
            }
        }

        var result = await _sitesService.MultiSearchSitesAsync(
            parseResult.UniqueDomains,
            parseResult.Duplicates,
            cancellationToken);

        var includeInternalFields = CanViewInternalSiteFields();
        var found = result.Found
            .Select(site => SitesMapper.ToSiteResponse(site, includeInternalFields))
            .ToList();
        var foundByDomain = found.ToDictionary(site => site.Domain, StringComparer.Ordinal);

        var response = new MultiSearchResponse
        {
            Results = parseResult.UniqueDomains
                .Select(domain => foundByDomain.TryGetValue(domain, out var site)
                    ? new MultiSearchResultResponse
                    {
                        Domain = domain,
                        Found = true,
                        Site = site
                    }
                    : new MultiSearchResultResponse
                    {
                        Domain = domain,
                        Found = false,
                        Site = null
                    })
                .ToList(),
            Found = found,
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
        if (IsLiteUser())
        {
            return Forbid();
        }

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
        if (IsLiteUser())
        {
            return Forbid();
        }

        var filterOptions = await _sitesService.GetFilterOptionsAsync(cancellationToken);

        return Ok(new FilterOptionsResponse
        {
            Niches = filterOptions.Niches
                .Select(option => new FilterOptionResponse
                {
                    Value = option.Value,
                    Label = option.Label
                })
                .ToList(),
            Locations = new LocationFilterOptionsResponse
            {
                Groups = filterOptions.Locations.Groups.Select(group => new LocationGroupFilterOptionResponse
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
                Locations = filterOptions.Locations.Locations.Select(location => new LocationFilterOptionResponse
                {
                    Key = location.Key,
                    DisplayName = location.DisplayName
                }).ToList(),
                Special = new LocationSpecialFilterOptionsResponse
                {
                    Unknown = new LocationFilterOptionResponse
                    {
                        Key = filterOptions.Locations.Special.Unknown.Key,
                        DisplayName = filterOptions.Locations.Special.Unknown.DisplayName
                    },
                    Other = filterOptions.Locations.Special.Other is null
                        ? null
                        : new LocationFilterOptionResponse
                    {
                        Key = filterOptions.Locations.Special.Other.Key,
                        DisplayName = filterOptions.Locations.Special.Other.DisplayName
                    }
                }
            },
            Terms = filterOptions.Terms
                .Select(term => new TermFilterOptionResponse
                {
                    TermKey = term.TermKey,
                    Label = term.Label,
                    TermType = term.TermType,
                    TermValue = term.TermValue,
                    TermUnit = term.TermUnit
                })
                .ToList()
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
            Pricing = request.Pricing is null
                ? null
                : new Application.Models.UpdateSitePricingRequest
                {
                    Prices = request.Pricing.Prices
                        .Select(price => new Application.Models.UpdateSitePriceOptionRequest
                        {
                            PriceType = price.PriceType,
                            TermKey = price.TermKey,
                            TermType = price.TermType,
                            TermValue = price.TermValue,
                            TermUnit = price.TermUnit,
                            AmountUsd = price.AmountUsd
                        })
                        .ToList(),
                    ServiceAvailabilities = request.Pricing.ServiceAvailabilities
                        .Select(availability => new Application.Models.UpdateSiteServiceAvailabilityRequest
                        {
                            ServiceType = availability.ServiceType,
                            Status = availability.Status
                        })
                        .ToList()
                },
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
    {
        var user = HttpContext?.User;
        return user is not null
            && !user.IsInRole(AppRoles.Client)
            && !user.IsInRole(AppRoles.Lite);
    }

    private bool IsLiteUser()
        => HttpContext?.User?.IsInRole(AppRoles.Lite) == true;

    private BadRequestObjectResult LiteMultiSearchUsageProblem(LiteMultiSearchUsageResult usage)
    {
        var status = usage.Status == LiteMultiSearchUsageStatus.MonthlyLimitExceeded
            ? StatusCodes.Status429TooManyRequests
            : StatusCodes.Status400BadRequest;
        var detail = usage.Status == LiteMultiSearchUsageStatus.MonthlyLimitExceeded
            ? LiteMultiSearchConstants.MonthlyLimitMessage
            : LiteMultiSearchConstants.PerRequestLimitMessage;

        return new BadRequestObjectResult(new ProblemDetails
        {
            Title = "Validation Error",
            Status = status,
            Detail = detail,
            Extensions =
            {
                ["domainsRequested"] = usage.DomainsRequested,
                ["domainsUsed"] = usage.DomainsUsed,
                ["monthlyLimit"] = usage.MonthlyLimit,
                ["remainingAfterRequest"] = usage.RemainingAfterRequest
            }
        })
        {
            StatusCode = status
        };
    }
}
