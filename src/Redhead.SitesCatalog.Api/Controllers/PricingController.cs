using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.Pricing;
using Redhead.SitesCatalog.Application.Services.Pricing;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/pricing")]
[Authorize(Policy = AppPolicies.PricingReadAccess)]
[Authorize(Policy = AppPolicies.LinkDetectivePricesReadAccess)]
public sealed class PricingController(PricingService service, PricingEditor editor) : ControllerBase
{
    [HttpGet]
    public Task<PricingPage> Search([FromQuery] PricingQuery request, CancellationToken cancellationToken) =>
        service.SearchAsync(request, cancellationToken);

    [HttpGet("terms")]
    public Task<IReadOnlyList<PricingTermDto>> Terms(CancellationToken cancellationToken) => service.GetTermsAsync(cancellationToken);

    [HttpGet("{domain}")]
    public async Task<ActionResult<PricingSiteDto>> Get(string domain, CancellationToken cancellationToken)
    {
        var row = await service.GetAsync(domain, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPut("{domain}")]
    [Authorize(Policy = AppPolicies.PricingEditAccess)]
    public async Task<IActionResult> Save(string domain, [FromBody] SavePricingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await editor.SaveAsync(domain, request, User.FindFirstValue(ClaimTypes.Email), cancellationToken);
            return NoContent();
        }
        catch (PricingConflictException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
