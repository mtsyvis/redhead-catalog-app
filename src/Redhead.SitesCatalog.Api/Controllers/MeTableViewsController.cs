using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models.TableViews;
using Redhead.SitesCatalog.Application.Models.TableViews;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me/table-views")]
public sealed class MeTableViewsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserTableViewsService _tableViewsService;

    public MeTableViewsController(
        UserManager<ApplicationUser> userManager,
        IUserTableViewsService tableViewsService)
    {
        _userManager = userManager;
        _tableViewsService = tableViewsService;
    }

    [HttpGet("{tableKey}")]
    public async Task<ActionResult<TableViewsResponseDto>> GetTableViews(
        string tableKey,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _tableViewsService.GetTableViewsAsync(
            user.Id,
            tableKey,
            cancellationToken);

        return Ok(response);
    }

    [HttpPut("{tableKey}/active")]
    public async Task<IActionResult> SetActiveView(
        string tableKey,
        [FromBody] SetActiveTableViewRequest request,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        await _tableViewsService.SetActiveViewAsync(
            user.Id,
            tableKey,
            request.ViewType,
            request.ViewKey,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{tableKey}/custom")]
    public async Task<ActionResult<TableCustomViewDto>> CreateCustomView(
        string tableKey,
        [FromBody] CreateTableCustomViewRequest request,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _tableViewsService.CreateCustomViewAsync(
            user.Id,
            tableKey,
            request.Name,
            request.Settings,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetTableViews),
            new { tableKey },
            response);
    }

    [HttpPut("{tableKey}/custom/{id:guid}")]
    public async Task<ActionResult<TableCustomViewDto>> UpdateCustomView(
        string tableKey,
        Guid id,
        [FromBody] UpdateTableCustomViewRequest request,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _tableViewsService.UpdateCustomViewAsync(
            user.Id,
            tableKey,
            id,
            request.Name,
            request.Settings,
            cancellationToken);

        return Ok(response);
    }

    [HttpDelete("{tableKey}/custom/{id:guid}")]
    public async Task<IActionResult> DeleteCustomView(
        string tableKey,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (IsLiteUser())
        {
            return Forbid();
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        await _tableViewsService.DeleteCustomViewAsync(
            user.Id,
            tableKey,
            id,
            cancellationToken);

        return NoContent();
    }

    private bool IsLiteUser()
        => HttpContext?.User?.IsInRole(AppRoles.Lite) == true;
}
