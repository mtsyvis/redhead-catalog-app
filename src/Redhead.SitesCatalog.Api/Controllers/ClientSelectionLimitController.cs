using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/users/{id}/selection-limit")]
[Authorize(Policy = AppPolicies.UsersManageAccess)]
public sealed class ClientSelectionLimitController(UserManager<ApplicationUser> users, IClientCatalogActivityService activity,
    ApplicationDbContext db, ClientCatalogBurstLimiter burstLimiter, TimeProvider clock) : ControllerBase
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
    public async Task<IActionResult> Update(string id, UpdateRequest request, CancellationToken cancellationToken = default)
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
        if (!result.Succeeded)
        {
            return Conflict(new { message = "The user changed. Reload and try again." });
        }
        if (trustChanged)
        {
            burstLimiter.Reset(user.Id);
        }
        if (trustChanged && nextTrusted)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var openAlerts = await db.ClientCatalogAlerts
                .Where(alert => alert.UserId == user.Id && alert.ReviewedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var alert in openAlerts)
            {
                alert.CloseForTrustedClient(now);
            }
            if (openAlerts.Count > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        return NoContent();
    }
}
