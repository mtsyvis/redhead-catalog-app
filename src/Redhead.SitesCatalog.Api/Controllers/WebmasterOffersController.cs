using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Api.Models;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers")]
[Authorize(Policy = AppPolicies.WebmasterOffersSummaryReadAccess)]
public sealed class WebmasterOffersController : ControllerBase
{
    private readonly IWebmasterOffersService _webmasterOffersService;
    private readonly ILogger<WebmasterOffersController> _logger;
    private readonly IGoogleDriveExportService _googleDriveExportService;

    public WebmasterOffersController(IWebmasterOffersService webmasterOffersService, ILogger<WebmasterOffersController> logger,
        IGoogleDriveExportService googleDriveExportService)
    {
        _webmasterOffersService = webmasterOffersService;
        _logger = logger;
        _googleDriveExportService = googleDriveExportService;
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
        [FromBody] WebmasterOffersExportRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _webmasterOffersService.GetExportDataAsync(request, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        LogExport(result, request.ExportAll, ExportConstants.DestinationDownload);
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "webmaster-offers.xlsx");
    }

    [HttpPost("export/google-drive")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportToGoogleDrive(
        [FromBody] WebmasterOffersExportRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _webmasterOffersService.GetExportDataAsync(request, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = WebmasterOffersExcelExportGenerator.Generate(result);
            var response = await _googleDriveExportService.UploadWorkbookAsync(userId, workbook,
                $"webmaster-offers-{DateTime.UtcNow:yyyy-MM-dd}.xlsx",
                result.Results.Sum(group => group.Offers.Count), cancellationToken);
            LogExport(result, request.ExportAll, ExportConstants.DestinationGoogleDrive);
            return Ok(response);
        }
        catch (GoogleDriveExportException exception)
        {
            var statusCode = exception.ErrorCode switch
            {
                GoogleDriveExportException.NotConnectedErrorCode or GoogleDriveExportException.ReconnectRequiredErrorCode => StatusCodes.Status409Conflict,
                GoogleDriveExportException.ConfigurationMissingErrorCode => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status502BadGateway
            };
            return StatusCode(statusCode, new ApiErrorResponse(exception.ErrorCode, statusCode));
        }
    }

    private void LogExport(WebmasterOffersMultiSearchResult<WebmasterOfferDto> result, bool exportAll, string destination)
        => _logger.LogInformation("Webmaster offers export by {UserId}: {DomainCount} domains, {OfferCount} offers, all: {ExportAll}, destination: {Destination}",
            User.FindFirstValue(ClaimTypes.NameIdentifier), result.UniqueDomainCount,
            result.Results.Sum(group => group.Offers.Count), exportAll, destination);

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
