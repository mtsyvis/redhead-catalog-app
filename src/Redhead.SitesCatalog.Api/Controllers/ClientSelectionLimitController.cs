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
    public sealed record UpdateRequest(
        [Range(1, ClientCatalogLimits.MaxSelectionLimit)] int? OverrideRows,
        bool? IsTrustedClient = null);
    public sealed record LimitResponse(int? OverrideRows, int? EffectiveRows, int DefaultRows, int MaxRows,
        bool IsTrustedClient,
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
        return Ok(new LimitResponse(
            user.IsTrustedClient ? null : user.ClientSelectionLimitOverride,
            user.IsTrustedClient ? null : ClientCatalogLimits.Resolve(user.ClientSelectionLimitOverride),
            ClientCatalogLimits.DefaultSelectionLimit,
            ClientCatalogLimits.MaxSelectionLimit,
            user.IsTrustedClient,
            windows));
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

        var nextTrusted = request.IsTrustedClient ?? user.IsTrustedClient;
        var trustChanged = user.IsTrustedClient != nextTrusted;
        if (user.IsTrustedClient && !nextTrusted)
        {
            user.ClientCatalogAutoBanResetAtUtc = clock.GetUtcNow().UtcDateTime;
        }
        user.IsTrustedClient = nextTrusted;
        user.ClientSelectionLimitOverride = nextTrusted ? null : request.OverrideRows;
        var result = await users.UpdateAsync(user);
        if (result.Succeeded && trustChanged)
        {
            burstLimiter.Reset(user.Id);
        }
        return result.Succeeded ? NoContent() : Conflict(new { message = "The user changed. Reload and try again." });
    }
}
