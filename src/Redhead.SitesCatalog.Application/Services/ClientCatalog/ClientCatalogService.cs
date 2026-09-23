using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

public sealed record ClientCatalogAccess(bool IsTrustedClient, int? SelectionLimit);

public interface IClientCatalogService
{
    Task<ClientCatalogAccess> GetAccessAsync(string userId, CancellationToken cancellationToken = default);
    Task EnsureBurstLimitAsync(string userId, IReadOnlyCollection<string> domains, int selectionLimit, bool consume = true, CancellationToken cancellationToken = default);
}

public sealed class ClientCatalogService(ApplicationDbContext context, ClientCatalogBurstLimiter burstLimiter) : IClientCatalogService
{
    public async Task EnsureBurstLimitAsync(string userId, IReadOnlyCollection<string> domains, int selectionLimit,
        bool consume = true, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new { user.ClientSelectionLimitOverride, user.IsTrustedClient })
            .SingleOrDefaultAsync(cancellationToken);
        if (user?.IsTrustedClient == true)
        {
            burstLimiter.Reset(userId);
            return;
        }
        var currentSelectionLimit = user is null ? selectionLimit : ClientCatalogLimits.Resolve(user.ClientSelectionLimitOverride);
        burstLimiter.EnsureAllowed(userId, domains, currentSelectionLimit, consume);
    }

    public async Task<ClientCatalogAccess> GetAccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.ClientSelectionLimitOverride, item.IsTrustedClient })
            .SingleOrDefaultAsync(cancellationToken);
        var isTrustedClient = user?.IsTrustedClient == true;
        return new ClientCatalogAccess(
            isTrustedClient,
            isTrustedClient ? null : ClientCatalogLimits.Resolve(user?.ClientSelectionLimitOverride));
    }

    public static void ValidateMultiSearch(int count, int selectionLimit)
    {
        if (count > selectionLimit)
        {
            throw new RequestValidationException($"Multi-search accepts at most {selectionLimit} unique domains for your account.");
        }
    }
}
