using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models.SavedFilters;
using Redhead.SitesCatalog.Application.Models.SavedFilters;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.SitesBrowseAccess)]
[Route("api/me/saved-filter-sets")]
public sealed class MeSavedFilterSetsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserSavedFilterSetsService _savedFilterSetsService;

    public MeSavedFilterSetsController(
        UserManager<ApplicationUser> userManager,
        IUserSavedFilterSetsService savedFilterSetsService)
    {
        _userManager = userManager;
        _savedFilterSetsService = savedFilterSetsService;
    }

    [HttpGet("{tableKey}")]
    public async Task<ActionResult<SavedFilterSetsResponseDto>> GetFilterSets(
        string tableKey,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _savedFilterSetsService.GetFilterSetsAsync(
            user.Id,
            tableKey,
            cancellationToken);

        return Ok(response);
    }

    [HttpPost("{tableKey}")]
    public async Task<ActionResult<SavedFilterSetDto>> CreateFilterSet(
        string tableKey,
        [FromBody] CreateSavedFilterSetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _savedFilterSetsService.CreateFilterSetAsync(
            user.Id,
            tableKey,
            request.Name,
            request.Settings,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetFilterSets),
            new { tableKey },
            response);
    }

    [HttpPut("{tableKey}/{id:guid}")]
    public async Task<ActionResult<SavedFilterSetDto>> UpdateFilterSet(
        string tableKey,
        Guid id,
        [FromBody] UpdateSavedFilterSetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var response = await _savedFilterSetsService.UpdateFilterSetAsync(
            user.Id,
            tableKey,
            id,
            request.Name,
            request.Settings,
            cancellationToken);

        return Ok(response);
    }

    [HttpDelete("{tableKey}/{id:guid}")]
    public async Task<IActionResult> DeleteFilterSet(
        string tableKey,
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        await _savedFilterSetsService.DeleteFilterSetAsync(
            user.Id,
            tableKey,
            id,
            cancellationToken);

        return NoContent();
    }
}
