using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers/linkdetective-prices")]
[Authorize(Policy = AppPolicies.WebmasterOffersReadAccess)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LinkDetectivePricesController(ILinkDetectivePricesService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> GetPrices(LinkDetectivePricesRequest request, CancellationToken cancellationToken)
    {
        var domain = DomainNormalizer.Normalize(request.Domain);
        if (string.IsNullOrEmpty(domain) || domain.Length > 253 || Uri.CheckHostName(domain) != UriHostNameType.Dns)
        {
            return BadRequest(new { message = "Enter one valid domain." });
        }

        var result = await service.GetAsync(domain, request.ForceRefresh, cancellationToken);
        return result is null ? NotFound(new { message = "Site not found in the catalog." }) : Ok(result);
    }
}

public sealed record LinkDetectivePricesRequest(string? Domain, bool ForceRefresh = false);
