using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/webmaster-offers/bulk-price-update")]
[Authorize(Policy = AppPolicies.WebmasterOffersReadAccess)]
[Authorize(Policy = AppPolicies.WebmasterOffersManageAccess)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BulkOfferPricePreviewController(BulkOfferPricePreviewService service, BulkOfferPriceOperationService operations,
    IGoogleDriveExportService googleDrive) : ControllerBase
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

    [HttpGet("operations/{id:guid}/update-report.xlsx")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    public async Task<IActionResult> UpdateReport(Guid id, CancellationToken cancellationToken)
        => await Download(id, false, cancellationToken);

    [HttpGet("operations/{id:guid}/catalog-calculation.xlsx")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    public async Task<IActionResult> CatalogCalculation(Guid id, CancellationToken cancellationToken)
        => await Download(id, true, cancellationToken);

    [HttpPost("operations/{id:guid}/update-report/google-drive")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    public async Task<IActionResult> UpdateReportToGoogleDrive(Guid id, CancellationToken cancellationToken)
        => await SaveToGoogleDrive(id, false, cancellationToken);

    [HttpPost("operations/{id:guid}/catalog-calculation/google-drive")]
    [Authorize(Policy = AppPolicies.WebmasterOffersExportAccess)]
    public async Task<IActionResult> CatalogCalculationToGoogleDrive(Guid id, CancellationToken cancellationToken)
        => await SaveToGoogleDrive(id, true, cancellationToken);

    private async Task<IActionResult> Download(Guid id, bool calculation, CancellationToken cancellationToken)
    {
        var bytes = await operations.DownloadAsync(id, calculation, cancellationToken);
        return bytes is null ? NotFound() : File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ReportFileName(id, calculation));
    }

    private async Task<IActionResult> SaveToGoogleDrive(Guid id, bool calculation, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var report = await operations.PrepareReportAsync(id, calculation, cancellationToken);
            if (report is null)
            {
                return NotFound();
            }
            cancellationToken.ThrowIfCancellationRequested();
            using var workbook = new MemoryStream(report.Content, writable: false);
            return Ok(await googleDrive.UploadWorkbookAsync(userId, workbook, ReportFileName(id, calculation),
                report.RowsExported, cancellationToken));
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

    private static string ReportFileName(Guid id, bool calculation)
        => $"{(calculation ? "catalog-calculation" : "price-update")}-{id:D}.xlsx";
}
