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
    /// Export sites as CSV with role-based limit enforcement
    /// </summary>
    [HttpGet("sites.csv")]
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

        var stream = await _exportService.ExportSitesAsCsvAsync(
            query,
            userId,
            userEmail,
            userRole,
            cancellationToken);

        return File(stream, ExportConstants.CsvContentType, ExportConstants.SitesFileName);
    }

    /// <summary>
    /// Export multi-search result as CSV: filtered Found rows (role limit) + all Not found domains.
    /// </summary>
    [HttpPost("sites-multi-search.csv")]
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

        Stream stream;
        try
        {
            stream = await _exportService.ExportMultiSearchAsCsvAsync(
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

        return File(stream, ExportConstants.CsvContentType, ExportConstants.SitesFileName);
    }
}
