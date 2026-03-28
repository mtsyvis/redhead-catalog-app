using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/role-settings")]
public class RoleSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RoleSettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.AdminAccess)]
    public async Task<ActionResult<IReadOnlyList<RoleSettingItemDto>>> GetRoleSettings(CancellationToken cancellationToken)
    {
        var list = await _context.RoleSettings
            .OrderBy(rs => rs.RoleName)
            .ToListAsync(cancellationToken);

        var result = list.Select(rs =>
        {
            var isSuperAdmin = string.Equals(rs.RoleName, AppRoles.SuperAdmin, StringComparison.Ordinal);
            return new RoleSettingItemDto(
                Role: rs.RoleName,
                ExportLimitMode: isSuperAdmin ? ExportLimitMode.Unlimited : rs.ExportLimitMode,
                ExportLimitRows: isSuperAdmin ? null : rs.ExportLimitRows,
                IsEditable: !isSuperAdmin);
        }).ToList();

        return Ok(result);
    }

    [HttpPut]
    [Authorize(Policy = AppPolicies.SuperAdminOnly)]
    public async Task<ActionResult> UpdateRoleSettings(
        [FromBody] IReadOnlyList<RoleSettingUpdateItemDto> request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.Count == 0)
        {
            return BadRequest(new MessageResponse("At least one role setting is required."));
        }

        foreach (var item in request)
        {
            var error = RoleSettingsValidation.ValidateUpdateItem(item);
            if (error != null)
            {
                return BadRequest(new MessageResponse(error));
            }
        }

        var existing = await _context.RoleSettings.ToListAsync(cancellationToken);
        var byRole = existing.ToDictionary(rs => rs.RoleName);

        foreach (var item in request)
        {
            if (byRole.TryGetValue(item.Role, out var entity))
            {
                entity.ExportLimitMode = item.ExportLimitMode!.Value;
                entity.ExportLimitRows = item.ExportLimitRows;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
