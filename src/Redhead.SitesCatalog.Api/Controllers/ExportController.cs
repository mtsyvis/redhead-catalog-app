using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Export;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Application.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly IExportService _exportService;
    private readonly IGoogleDriveExportService _googleDriveExportService;

    public ExportController(
        IExportService exportService,
        IGoogleDriveExportService googleDriveExportService)
    {
        _exportService = exportService;
        _googleDriveExportService = googleDriveExportService;
    }

    /// <summary>
    /// Export sites as Excel with filters in the request body.
    /// </summary>
    [HttpPost("sites.xlsx")]
    public async Task<IActionResult> ExportSitesFromBody(
        [FromBody] ExportSitesRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest();
        }

        return await ExportSitesCore(request, cancellationToken);
    }

    /// <summary>
    /// Export sites as Excel and upload the same workbook to the connected user's Google Drive export folder.
    /// Accepts the same body as /api/export/sites.xlsx.
    /// Multi-search exports may use SearchText plus optional Filters, matching /api/export/sites-multi-search.xlsx.
    /// </summary>
    [HttpPost("/api/sites/export/google-drive")]
    public async Task<IActionResult> ExportSitesToGoogleDrive(
        [FromBody] GoogleDriveExportRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest();
        }

        try
        {
            var userContext = GetRequiredUserContext();
            var result = request.SearchText != null
                ? await ExportMultiSearchToGoogleDriveAsync(request, userContext, cancellationToken)
                : await ExportSitesToGoogleDriveAsync(request, userContext, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("User information not found");
        }
        catch (RequestValidationException ex)
        {
            return ValidationProblem(ex);
        }
        catch (GoogleDriveExportException ex)
        {
            var statusCode = GetGoogleDriveExportStatusCode(ex);
            return StatusCode(
                statusCode,
                new ApiErrorResponse(ex.ErrorCode, statusCode));
        }
    }

    /// <summary>
    /// Export multi-search result as Excel: filtered Found rows (effective policy limit) + matching Not found domains.
    /// </summary>
    [HttpPost("sites-multi-search.xlsx")]
    public async Task<IActionResult> ExportSitesMultiSearch(
        [FromBody] ExportMultiSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest();
        }

        try
        {
            var userContext = GetRequiredUserContext();
            var query = ToMultiSearchQuery(request);
            var result = await _exportService.ExportMultiSearchAsExcelAsync(
                request.SearchText,
                query,
                userContext.UserId,
                userContext.UserEmail,
                userContext.UserRole,
                request.VisibleColumnKeys,
                cancellationToken);

            AddExportHeaders(result);

            return File(result.FileStream, ExportConstants.ExcelContentType, ExportConstants.SitesFileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("User information not found");
        }
        catch (RequestValidationException ex)
        {
            return ValidationProblem(ex);
        }
    }

    private async Task<IActionResult> ExportSitesCore(
        ExportSitesRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userContext = GetRequiredUserContext();
            var query = ToSitesQuery(request);
            var result = await _exportService.ExportSitesAsExcelAsync(
                query,
                userContext.UserId,
                userContext.UserEmail,
                userContext.UserRole,
                request.VisibleColumnKeys,
                cancellationToken);

            AddExportHeaders(result);

            return File(result.FileStream, ExportConstants.ExcelContentType, ExportConstants.SitesFileName);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized("User information not found");
        }
        catch (RequestValidationException ex)
        {
            return ValidationProblem(ex);
        }
    }

    private async Task<GoogleDriveExportResponse> ExportSitesToGoogleDriveAsync(
        GoogleDriveExportRequest request,
        UserExportContext userContext,
        CancellationToken cancellationToken)
    {
        var query = ToSitesQuery(request);

        return await _googleDriveExportService.ExportSitesAsync(
            query,
            userContext.UserId,
            userContext.UserEmail,
            userContext.UserRole,
            request.VisibleColumnKeys,
            cancellationToken);
    }

    private async Task<GoogleDriveExportResponse> ExportMultiSearchToGoogleDriveAsync(
        GoogleDriveExportRequest request,
        UserExportContext userContext,
        CancellationToken cancellationToken)
    {
        var query = ToMultiSearchQuery(request);

        return await _googleDriveExportService.ExportMultiSearchAsync(
            request.SearchText ?? string.Empty,
            query,
            userContext.UserId,
            userContext.UserEmail,
            userContext.UserRole,
            request.VisibleColumnKeys,
            cancellationToken);
    }

    private static SitesQuery ToMultiSearchQuery(ExportMultiSearchRequest request)
        => ToMultiSearchQuery(request.Filters);

    private static SitesQuery ToMultiSearchQuery(GoogleDriveExportRequest request)
        => ToMultiSearchQuery(request.Filters);

    private static SitesQuery ToMultiSearchQuery(SitesQueryRequest? filters)
    {
        if (filters?.StopListDomains is { Count: > 0 })
        {
            throw new RequestValidationException(StopListConstants.MultiSearchNotSupportedMessage);
        }

        var query = filters != null ? SitesMapper.ToQuery(filters) : new SitesQuery();
        query.Search = null;
        return query;
    }

    private static SitesQuery ToSitesQuery(ExportSitesRequest request)
        => SitesMapper.ToQuery(request.Filters ?? new SitesQueryRequest());

    private static SitesQuery ToSitesQuery(GoogleDriveExportRequest request)
        => SitesMapper.ToQuery(request.Filters ?? new SitesQueryRequest());

    private UserExportContext GetRequiredUserContext()
    {
        var user = ControllerContext.HttpContext?.User;
        if (user == null)
        {
            throw new UnauthorizedAccessException();
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = user.FindFirstValue(ClaimTypes.Email);
        var userRole = user.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userRole))
        {
            throw new UnauthorizedAccessException();
        }

        return new UserExportContext(userId, userEmail, userRole);
    }

    private BadRequestObjectResult ValidationProblem(RequestValidationException ex)
        => BadRequest(new ProblemDetails
        {
            Title = "Validation Error",
            Status = StatusCodes.Status400BadRequest,
            Detail = ex.Message
        });

    private static int GetGoogleDriveExportStatusCode(GoogleDriveExportException ex)
        => ex.ErrorCode switch
        {
            GoogleDriveExportException.NotConnectedErrorCode => StatusCodes.Status409Conflict,
            GoogleDriveExportException.ReconnectRequiredErrorCode => StatusCodes.Status409Conflict,
            GoogleDriveExportException.ConfigurationMissingErrorCode => StatusCodes.Status500InternalServerError,
            GoogleDriveExportException.UploadFailedErrorCode => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

    private void AddExportHeaders(ExportResult result)
    {
        Response.Headers["X-Export-Requested-Rows"] = result.RequestedRows.ToString();
        Response.Headers["X-Export-Exported-Rows"] = result.ExportedRows.ToString();
        Response.Headers["X-Export-Truncated"] = result.Truncated ? "true" : "false";
        if (result.LimitRows.HasValue)
        {
            Response.Headers["X-Export-Limit-Rows"] = result.LimitRows.Value.ToString();
        }
    }

    private readonly record struct UserExportContext(string UserId, string UserEmail, string UserRole);
}
