using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/role-settings")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class RoleSettingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public RoleSettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleSettingItemDto>>> GetRoleSettings(CancellationToken cancellationToken)
    {
        var list = await _context.RoleSettings
            .OrderBy(rs => rs.RoleName)
            .Select(rs => new RoleSettingItemDto(rs.RoleName, rs.ExportLimitRows))
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPut]
    public async Task<ActionResult> UpdateRoleSettings(
        [FromBody] IReadOnlyList<RoleSettingUpdateItemDto> request,
        CancellationToken cancellationToken)
    {
        if (request == null || request.Count == 0)
        {
            return BadRequest(new MessageResponse("At least one role setting is required."));
        }

        var allowedRoles = new HashSet<string>(AppRoles.All);
        foreach (var item in request)
        {
            if (!allowedRoles.Contains(item.Role))
            {
                return BadRequest(new MessageResponse($"Invalid role: {item.Role}."));
            }
        }

        var existing = await _context.RoleSettings.ToListAsync(cancellationToken);
        var byRole = existing.ToDictionary(rs => rs.RoleName);

        foreach (var item in request)
        {
            if (byRole.TryGetValue(item.Role, out var entity))
            {
                entity.ExportLimitRows = item.ExportLimitRows;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
