using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers/bulk-price-update")]
[Authorize(Policy = AppPolicies.WebmasterOffersReadAccess)]
[Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BulkOfferPricePreviewController(BulkOfferPricePreviewService service) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<BulkOfferPricePreviewResult>> Preview(BulkOfferPricePreviewRequest request,
        CancellationToken cancellationToken) => Ok(await service.PreviewAsync(request, cancellationToken));
}
