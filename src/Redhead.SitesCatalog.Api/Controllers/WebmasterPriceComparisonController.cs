using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers/price-comparison")]
[Authorize(Policy = AppPolicies.WebmasterPriceComparisonReadAccess)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WebmasterPriceComparisonController(IWebmasterPriceComparisonService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByDomain([FromQuery] string? domain, CancellationToken cancellationToken)
    {
        var normalized = DomainNormalizer.Normalize(domain);
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 253 || Uri.CheckHostName(normalized) != UriHostNameType.Dns)
        {
            return BadRequest(new { message = "Enter one valid domain." });
        }

        return Ok(await service.GetByDomainAsync(normalized, cancellationToken));
    }
}
