using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/users/{id}/selection-limit")]
[Authorize(Policy = AppPolicies.UsersManageAccess)]
public sealed class ClientSelectionLimitController(UserManager<ApplicationUser> users, IClientCatalogActivityService activity,
    ClientCatalogBurstLimiter burstLimiter, TimeProvider clock) : ControllerBase
{
    public sealed record UpdateRequest([Range(1, ClientCatalogLimits.MaxSelectionLimit)] int? OverrideRows);
    public sealed record LimitResponse(int? OverrideRows, int EffectiveRows, int DefaultRows, int MaxRows,
        IReadOnlyList<ClientCatalogActivityWindow> Activity);

    [HttpGet]
    public async Task<ActionResult<LimitResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await users.IsInRoleAsync(user, AppRoles.Client))
        {
            return BadRequest(new { message = "Selection limits apply only to Client accounts." });
        }

        var windows = await activity.GetActivityAsync(id, cancellationToken);
        return Ok(new LimitResponse(user.ClientSelectionLimitOverride, ClientCatalogLimits.Resolve(user.ClientSelectionLimitOverride),
            ClientCatalogLimits.DefaultSelectionLimit, ClientCatalogLimits.MaxSelectionLimit, windows));
    }

    [HttpPut]
    public async Task<IActionResult> Update(string id, UpdateRequest request)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await users.IsInRoleAsync(user, AppRoles.Client))
        {
            return BadRequest(new { message = "Selection limits apply only to Client accounts." });
        }

        if (request.OverrideRows is < 1 or > ClientCatalogLimits.MaxSelectionLimit)
        {
            return BadRequest(new { message = $"Enter a whole number between 1 and {ClientCatalogLimits.MaxSelectionLimit}." });
        }

        var previousLimit = ClientCatalogLimits.Resolve(user.ClientSelectionLimitOverride);
        var nextLimit = ClientCatalogLimits.Resolve(request.OverrideRows);
        var wasExempt = ClientCatalogLimits.IsBurstExempt(previousLimit);
        var isExempt = ClientCatalogLimits.IsBurstExempt(nextLimit);
        var resetBurst = wasExempt || isExempt;

        user.ClientSelectionLimitOverride = request.OverrideRows;
        if (wasExempt && !isExempt)
        {
            user.ClientCatalogAutoBanResetAtUtc = clock.GetUtcNow().UtcDateTime;
        }
        var result = await users.UpdateAsync(user);
        if (result.Succeeded && resetBurst)
        {
            burstLimiter.Reset(user.Id);
        }
        return result.Succeeded ? NoContent() : Conflict(new { message = "The user changed. Reload and try again." });
    }
}
