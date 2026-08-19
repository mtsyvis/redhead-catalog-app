using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Application.Validation;
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

    [HttpGet("{offerId:guid}")]
    [Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
    public async Task<ActionResult<WebmasterOfferEditDto>> GetForEdit(
        Guid offerId,
        CancellationToken cancellationToken)
    {
        var result = await _webmasterOffersService.GetForEditAsync(offerId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut("{offerId:guid}")]
    [Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
    public async Task<ActionResult<WebmasterOfferDto>> Update(
        Guid offerId,
        [FromBody] UpdateWebmasterOfferRequest request,
        CancellationToken cancellationToken)
    {
        var validation = WebmasterOfferWriteValidator.ValidateAndNormalize(request);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                message = "Validation failed",
                fieldErrors = validation.FieldErrors
            });
        }

        var result = await _webmasterOffersService.UpdateAsync(
            offerId,
            validation.NormalizedRequest!,
            HttpContext.User.FindFirstValue(ClaimTypes.Email),
            cancellationToken);

        return result.Status switch
        {
            WebmasterOfferUpdateStatus.Success => Ok(result.Offer),
            WebmasterOfferUpdateStatus.NotFound => NotFound(),
            WebmasterOfferUpdateStatus.Conflict => Conflict(new
            {
                message = "This offer was changed by another user. Reload it and try again."
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpGet("{offerId:guid}/history")]
    [Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
    public async Task<ActionResult<IReadOnlyList<Application.Models.ChangeHistory.EntityChangeHistoryDto>>> GetHistory(
        Guid offerId,
        CancellationToken cancellationToken)
    {
        var history = await _webmasterOffersService.GetHistoryAsync(offerId, cancellationToken);
        return Ok(history);
    }
}
