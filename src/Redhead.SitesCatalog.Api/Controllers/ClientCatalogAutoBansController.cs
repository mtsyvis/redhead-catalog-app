using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/catalog-auto-bans")]
[Authorize(Policy = AppPolicies.UsersReadAccess)]
public sealed class ClientCatalogAutoBansController(ApplicationDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await db.ClientCatalogAutoBans.AsNoTracking()
            .Where(autoBan => autoBan.ReviewedAtUtc == null)
            .OrderByDescending(autoBan => autoBan.DetectedAtUtc)
            .Select(autoBan => new
            {
                autoBan.Id,
                autoBan.UserId,
                Email = db.Users.Where(user => user.Id == autoBan.UserId).Select(user => user.Email).FirstOrDefault(),
                Role = (from userRole in db.UserRoles
                        join role in db.Roles on userRole.RoleId equals role.Id
                        where userRole.UserId == autoBan.UserId
                        select role.Name).FirstOrDefault(),
                IsGoogleOnly = db.Users.Where(user => user.Id == autoBan.UserId)
                    .Any(user => user.PasswordHash == null && db.UserLogins.Any(login =>
                        login.UserId == user.Id && login.LoginProvider == ExternalLoginProviders.Google)),
                autoBan.DetectedAtUtc,
                autoBan.UniqueSites,
                autoBan.Threshold,
                autoBan.EmailSentAtUtc
            })
            .ToListAsync(cancellationToken));

    [HttpPost("{id:long}/review")]
    [Authorize(Policy = AppPolicies.UsersManageAccess, Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Review(long id, CancellationToken cancellationToken)
    {
        var autoBan = await db.ClientCatalogAutoBans.SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (autoBan is null)
        {
            return NotFound();
        }
        if (autoBan.ReviewedAtUtc is null)
        {
            var reviewedAt = clock.GetUtcNow().UtcDateTime;
            var reviewedBy = User.FindFirstValue(ClaimTypes.NameIdentifier);
            autoBan.ReviewedAtUtc = reviewedAt;
            autoBan.ReviewedByUserId = reviewedBy;
            var alerts = await db.ClientCatalogAlerts
                .Where(alert => alert.UserId == autoBan.UserId && alert.ReviewedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var alert in alerts)
            {
                alert.ReviewedAtUtc = reviewedAt;
                alert.ReviewedByUserId = reviewedBy;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
        return NoContent();
    }
}
