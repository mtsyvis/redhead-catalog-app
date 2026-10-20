using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers")]
[Authorize(Policy = AppPolicies.WebmasterOffersSummaryReadAccess)]
public sealed class WebmasterOffersController : ControllerBase
{
    private readonly IWebmasterOffersService _webmasterOffersService;
    private readonly ILogger<WebmasterOffersController> _logger;

    public WebmasterOffersController(IWebmasterOffersService webmasterOffersService, ILogger<WebmasterOffersController> logger)
    {
        _webmasterOffersService = webmasterOffersService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetByDomain(
        [FromQuery] string? domain,
        CancellationToken cancellationToken)
    {
        if (!CanReadFullOffers())
        {
            var summary = await _webmasterOffersService.GetSummaryByDomainAsync(domain, cancellationToken);
            return Ok(summary);
        }

        var result = await _webmasterOffersService.GetByDomainAsync(domain, cancellationToken);
        return Ok(result);
    }

    [HttpPost("multi-search")]
    public async Task<IActionResult> MultiSearch(
        [FromBody] WebmasterOffersMultiSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanReadFullOffers())
        {
            return Ok(await _webmasterOffersService.MultiSearchSummaryAsync(request.QueryText, cancellationToken));
        }

        return Ok(await _webmasterOffersService.MultiSearchAsync(request.QueryText, cancellationToken));
    }

    [HttpPost("export.xlsx")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Export(
        [FromBody] WebmasterOffersMultiSearchRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _webmasterOffersService.MultiSearchAsync(request.QueryText, cancellationToken);
        var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        _logger.LogInformation("Webmaster offers export by {UserId}: {DomainCount} domains, {OfferCount} offers",
            User.FindFirstValue(ClaimTypes.NameIdentifier), result.UniqueDomainCount,
            result.Results.Sum(group => group.Offers.Count));
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "webmaster-offers.xlsx");
    }

    private bool CanReadFullOffers()
        => User.FindAll(ClaimTypes.Role)
            .Any(claim => RolePermissionMatrix.HasPermission(claim.Value, AppPermissions.WebmasterOffersRead));

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
                message = "This offer or its linked webmaster was changed by another user. Reload it and try again."
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
