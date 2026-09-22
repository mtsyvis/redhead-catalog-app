using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/catalog-alerts")]
[Authorize(Policy = AppPolicies.UsersReadAccess)]
public sealed class ClientCatalogAlertsController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await db.ClientCatalogAlerts.AsNoTracking().Where(alert => alert.ReviewedAtUtc == null)
            .OrderByDescending(alert => alert.DetectedAtUtc)
            .Select(alert => new { alert.Id, alert.UserId,
                Email = db.Users.Where(user => user.Id == alert.UserId).Select(user => user.Email).FirstOrDefault(),
                alert.DetectedAtUtc, alert.UniqueSites, alert.Threshold, alert.EmailSentAtUtc })
            .ToListAsync(cancellationToken));

    [HttpPost("{id:long}/review")]
    [Authorize(Policy = AppPolicies.UsersManageAccess, Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Review(long id, CancellationToken cancellationToken)
    {
        var alert = await db.ClientCatalogAlerts.SingleOrDefaultAsync(alert => alert.Id == id, cancellationToken);
        if (alert is null)
        {
            return NotFound();
        }
        if (alert.ReviewedAtUtc is null)
        {
            alert.ReviewedAtUtc = clock.GetUtcNow().UtcDateTime;
            alert.ReviewedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }
}
