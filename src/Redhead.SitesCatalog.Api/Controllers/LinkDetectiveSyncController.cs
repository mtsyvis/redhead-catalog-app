using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Application.LinkDetective;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/linkdetective-sync")]
[Authorize(Policy = AppPolicies.LinkDetectiveSyncReadAccess)]
public sealed class LinkDetectiveSyncController(LinkDetectiveSyncMonitoring monitoring) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult> Status(CancellationToken cancellationToken) => Ok(await monitoring.GetStatusAsync(cancellationToken));

    [HttpGet("runs")]
    public async Task<ActionResult> Runs(CancellationToken cancellationToken, [FromQuery] int page = PaginationDefaults.DefaultPage, [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize)
    {
        if (!ValidPage(page, pageSize))
        {
            return BadRequest($"Provide a positive page and a page size between 1 and {PaginationDefaults.MaxTablePageSize}.");
        }

        return Ok(await monitoring.ListRunsAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("runs/{id:guid}")]
    public async Task<ActionResult> Run(Guid id, CancellationToken cancellationToken, [FromQuery] int page = PaginationDefaults.DefaultPage,
        [FromQuery] int pageSize = PaginationDefaults.DefaultPageSize, [FromQuery] string? domain = null, [FromQuery] LinkDetectiveSyncItemStatus? status = null)
    {
        if (!ValidPage(page, pageSize) || domain?.Length > 253 || status is not null && !Enum.IsDefined(status.Value))
        {
            return BadRequest("Invalid pagination, domain search, or result status.");
        }

        var result = await monitoring.GetRunAsync(id, page, pageSize, domain, status, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private static bool ValidPage(int page, int pageSize) => page > 0 && pageSize is >= 1 and <= PaginationDefaults.MaxTablePageSize && (long)(page - 1) * pageSize <= int.MaxValue;
}
