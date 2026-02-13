using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Mappers;
using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

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
        // Get user information from claims
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var userRole = User.FindFirstValue(ClaimTypes.Role);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail) || string.IsNullOrEmpty(userRole))
        {
            return Unauthorized("User information not found");
        }

        // Convert request to query
        var query = SitesMapper.ToQuery(request);

        // Export sites (exceptions handled by GlobalExceptionHandler)
        var stream = await _exportService.ExportSitesAsCsvAsync(
            query,
            userId,
            userEmail,
            userRole,
            cancellationToken);

        // Return CSV file
        return File(stream, ExportConstants.CsvContentType, ExportConstants.SitesFileName);
    }
}
