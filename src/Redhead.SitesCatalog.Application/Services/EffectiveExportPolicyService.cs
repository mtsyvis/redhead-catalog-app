using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class EffectiveExportPolicyService : IEffectiveExportPolicyService
{
    private readonly ApplicationDbContext _context;

    public EffectiveExportPolicyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EffectiveExportPolicy> GetEffectivePolicyAsync(
        string userId,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync(new object?[] { userId }, cancellationToken);
        return await GetEffectivePolicyAsync(user, userRole, cancellationToken);
    }

    public async Task<EffectiveExportPolicy> GetEffectivePolicyAsync(
        ApplicationUser? user,
        string userRole,
        CancellationToken cancellationToken = default)
    {
        var roleSettings = await _context.RoleSettings
            .FirstOrDefaultAsync(rs => rs.RoleName == userRole, cancellationToken);

        if (roleSettings == null)
        {
            throw new RoleSettingsNotFoundException(userRole);
        }

        return EffectiveExportPolicyResolver.Resolve(userRole, roleSettings, user);
    }
}
