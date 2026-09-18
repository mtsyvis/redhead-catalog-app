using Redhead.SitesCatalog.Application.Services.Analytics.MissingDomainsAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Api.Validation;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/analytics/missing-domains")]
[Authorize(Policy = AppPolicies.MissingDomainsAnalyticsReadAccess)]
public sealed class MissingDomainsAnalyticsController(IMissingDomainsAnalyticsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<MissingDomainsAnalyticsDto>> Get(
        [FromQuery] MissingDomainsAnalyticsRequest request, CancellationToken cancellationToken)
    {
        var mapping = AnalyticsRequestMapper.ToMissingDomainsQuery(request, DateTimeOffset.UtcNow);
        if (mapping.Error != null)
        {
            return BadRequest(new MessageResponse(mapping.Error));
        }

        return Ok(await service.GetAsync(mapping.Query!, cancellationToken));
    }
}
