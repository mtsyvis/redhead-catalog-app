using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Api.Validation;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = AppPolicies.AnalyticsReadAccess)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IBusinessDemandAnalyticsService _businessDemandAnalyticsService;
    private readonly IExportActivityAnalyticsService _exportActivityAnalyticsService;

    public AnalyticsController(
        IBusinessDemandAnalyticsService businessDemandAnalyticsService,
        IExportActivityAnalyticsService exportActivityAnalyticsService)
    {
        _businessDemandAnalyticsService = businessDemandAnalyticsService;
        _exportActivityAnalyticsService = exportActivityAnalyticsService;
    }

    [HttpGet("business-demand")]
    public async Task<ActionResult<BusinessDemandAnalyticsDto>> GetBusinessDemand(
        [FromQuery] BusinessDemandAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = AnalyticsRequestMapper.ToBusinessDemandQuery(
            request,
            DateTimeOffset.UtcNow);
        if (mapping.Error != null)
        {
            return BadRequest(new MessageResponse(mapping.Error));
        }

        var result = await _businessDemandAnalyticsService.GetBusinessDemandAsync(
            mapping.Query!,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("export-activity")]
    public async Task<ActionResult<ExportActivityAnalyticsDto>> GetExportActivity(
        [FromQuery] ExportActivityAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = AnalyticsRequestMapper.ToExportActivityQuery(
            request,
            DateTimeOffset.UtcNow);
        if (mapping.Error != null)
        {
            return BadRequest(new MessageResponse(mapping.Error));
        }

        var result = await _exportActivityAnalyticsService.GetExportActivityAsync(
            mapping.Query!,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("logs/{id:guid}")]
    public async Task<ActionResult<ExportLogDetailsDto>> GetExportLogDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _exportActivityAnalyticsService.GetExportLogDetailsAsync(
            id,
            cancellationToken);
        if (result == null)
        {
            return NotFound(new MessageResponse("Export log not found."));
        }

        return Ok(result);
    }

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsClientOptionDto>>> GetClientOptions(
        CancellationToken cancellationToken)
    {
        var result = await _businessDemandAnalyticsService.ListClientOptionsAsync(cancellationToken);
        return Ok(result);
    }
}
