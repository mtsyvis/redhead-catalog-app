using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
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
public sealed class BulkOfferPricePreviewController(BulkOfferPricePreviewService service, BulkOfferPriceOperationService operations) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<ActionResult<BulkOfferPricePreviewResult>> Preview(BulkOfferPricePreviewRequest request,
        CancellationToken cancellationToken) => Ok(await service.PreviewAsync(request, cancellationToken));

    [HttpPost("apply")]
    public async Task<ActionResult<BulkOfferPriceOperationResult>> Apply(BulkOfferPriceApplyRequest request,
        CancellationToken cancellationToken)
    {
        try { return Ok(await operations.ApplyAsync(request, User.FindFirstValue(ClaimTypes.Email), cancellationToken)); }
        catch (BulkOfferPriceConflictException error) { return Conflict(new { message = error.Message }); }
    }

    [HttpGet("operations")]
    public async Task<ActionResult<IReadOnlyList<BulkOfferPriceOperationSummary>>> Recent(CancellationToken cancellationToken)
        => Ok(await operations.RecentAsync(cancellationToken));

    [HttpGet("operations/{id:guid}")]
    public async Task<ActionResult<BulkOfferPriceOperationResult>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await operations.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
