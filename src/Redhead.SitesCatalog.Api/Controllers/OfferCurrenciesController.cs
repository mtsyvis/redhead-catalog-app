using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers/currencies")]
[Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OfferCurrenciesController(OfferCurrencyService currencies) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await currencies.GetCurrenciesAsync(cancellationToken));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(OfferConversionRequest request, CancellationToken cancellationToken)
        => Ok(await currencies.ConvertAsync(request, false, cancellationToken));
}
