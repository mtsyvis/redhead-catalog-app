using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers")]
[Authorize(Policy = AppPolicies.WebmasterOffersReadAccess)]
public sealed class WebmasterOffersController : ControllerBase
{
    private readonly IWebmasterOffersService _webmasterOffersService;

    public WebmasterOffersController(IWebmasterOffersService webmasterOffersService)
    {
        _webmasterOffersService = webmasterOffersService;
    }

    [HttpGet]
    public async Task<ActionResult<WebmasterOffersSearchResult>> GetByDomain(
        [FromQuery] string? domain,
        CancellationToken cancellationToken)
    {
        var result = await _webmasterOffersService.GetByDomainAsync(domain, cancellationToken);
        return Ok(result);
    }
}
