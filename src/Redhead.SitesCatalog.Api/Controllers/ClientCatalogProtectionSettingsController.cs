using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/client-catalog-protection")]
[Authorize(Policy = AppPolicies.UsersManageAccess, Roles = AppRoles.SuperAdmin)]
public sealed class ClientCatalogProtectionSettingsController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClientCatalogProtectionSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        var settings = await db.ClientCatalogProtectionSettings.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == ClientCatalogProtectionSettings.SingletonId, cancellationToken);
        return Ok(await ToResponseAsync(settings ?? new ClientCatalogProtectionSettings(), cancellationToken));
    }

    [HttpPut]
    public async Task<ActionResult<ClientCatalogProtectionSettingsResponse>> Update(
        ClientCatalogProtectionSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AutoBanUniqueSitesPer24Hours <= 0)
        {
            return BadRequest(new MessageResponse("The 24-hour automatic-ban threshold must be a positive whole number."));
        }

        var settings = await db.ClientCatalogProtectionSettings
            .SingleOrDefaultAsync(row => row.Id == ClientCatalogProtectionSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = new ClientCatalogProtectionSettings();
            db.ClientCatalogProtectionSettings.Add(settings);
        }
        settings.AutoBanEnabled = request.AutoBanEnabled;
        settings.AutoBanUniqueSitesPer24Hours = request.AutoBanUniqueSitesPer24Hours;
        settings.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        settings.UpdatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ToResponseAsync(settings, cancellationToken));
    }

    private async Task<ClientCatalogProtectionSettingsResponse> ToResponseAsync(
        ClientCatalogProtectionSettings settings,
        CancellationToken cancellationToken)
    {
        var updatedBy = settings.UpdatedByUserId is null
            ? null
            : await db.Users.AsNoTracking()
                .Where(user => user.Id == settings.UpdatedByUserId)
                .Select(user => new { user.DisplayName, user.Email })
                .SingleOrDefaultAsync(cancellationToken);
        var updatedByDisplayName = string.IsNullOrWhiteSpace(updatedBy?.DisplayName)
            ? updatedBy?.Email
            : updatedBy.DisplayName.Trim();

        return new ClientCatalogProtectionSettingsResponse(
            settings.AutoBanEnabled,
            settings.AutoBanUniqueSitesPer24Hours,
            settings.UpdatedAtUtc,
            settings.UpdatedByUserId,
            updatedByDisplayName);
    }
}
