using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterSearch;
using Redhead.SitesCatalog.Application.Services.WebmasterSearch;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmasters")]
[Authorize(Policy = AppPolicies.WebmasterOffersReadAccess)]
public sealed class WebmastersController : ControllerBase
{
    private readonly IWebmasterSearchService _webmasterSearchService;

    public WebmastersController(IWebmasterSearchService webmasterSearchService)
    {
        _webmasterSearchService = webmasterSearchService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<WebmasterSearchResult>> Search(
        [FromQuery] string? contact,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = WebmasterSearchService.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _webmasterSearchService.SearchAsync(
            contact,
            page,
            pageSize,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{webmasterId:guid}/workspace")]
    public async Task<ActionResult<WebmasterWorkspaceDto>> GetWorkspace(
        Guid webmasterId,
        CancellationToken cancellationToken)
    {
        var result = await _webmasterSearchService.GetWorkspaceAsync(webmasterId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
