using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Export;
using Redhead.SitesCatalog.Api.Models.Sites;
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

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Export sites as Excel with effective export policy enforcement.
    /// </summary>
    [HttpGet("sites.xlsx")]
    public async Task<IActionResult> ExportSites(
        [FromQuery] SitesQueryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userRole))
        {
            return Unauthorized("User information not found");
        }

        SitesQuery query;
        try
        {
            query = SitesMapper.ToQuery(request);
        }
        catch (RequestValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }

        var result = await _exportService.ExportSitesAsExcelAsync(
            query,
            userId,
            userEmail,
            userRole,
            cancellationToken);

        AddExportHeaders(result);

        return File(result.FileStream, ExportConstants.ExcelContentType, ExportConstants.SitesFileName);
    }

    /// <summary>
    /// Export multi-search result as Excel: filtered Found rows (effective policy limit) + matching Not found domains.
    /// </summary>
    [HttpPost("sites-multi-search.xlsx")]
    public async Task<IActionResult> ExportSitesMultiSearch(
        [FromBody] ExportMultiSearchRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userRole))
        {
            return Unauthorized("User information not found");
        }

        if (request == null)
        {
            return BadRequest();
        }

        SitesQuery query;
        try
        {
            query = request.Filters != null ? SitesMapper.ToQuery(request.Filters) : new SitesQuery();
        }
        catch (RequestValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }

        query.Search = null;
        query.SortBy = request.SortBy ?? query.SortBy;
        query.SortDir = request.SortDir ?? query.SortDir;

        ExportResult result;
        try
        {
            result = await _exportService.ExportMultiSearchAsExcelAsync(
                request.QueryText,
                query,
                userId,
                userEmail,
                userRole,
                cancellationToken);
        }
        catch (RequestValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message
            });
        }

        AddExportHeaders(result);

        return File(result.FileStream, ExportConstants.ExcelContentType, ExportConstants.SitesFileName);
    }

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
}
