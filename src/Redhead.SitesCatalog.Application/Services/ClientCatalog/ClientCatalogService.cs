using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services.ClientCatalog;

public interface IClientCatalogService
{
    Task<int> GetSelectionLimitAsync(string userId, CancellationToken cancellationToken = default);
    Task EnsureBurstLimitAsync(string userId, IReadOnlyCollection<string> domains, int selectionLimit, bool consume = true, CancellationToken cancellationToken = default);
}

public sealed class ClientCatalogService(ApplicationDbContext context, ClientCatalogBurstLimiter burstLimiter) : IClientCatalogService
{
    public async Task EnsureBurstLimitAsync(string userId, IReadOnlyCollection<string> domains, int selectionLimit,
        bool consume = true, CancellationToken cancellationToken = default)
    {
        var user = await context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new { user.ClientSelectionLimitOverride })
            .SingleOrDefaultAsync(cancellationToken);
        var currentSelectionLimit = user is null ? selectionLimit : ClientCatalogLimits.Resolve(user.ClientSelectionLimitOverride);
        burstLimiter.EnsureAllowed(userId, domains, currentSelectionLimit, consume);
    }

    public async Task<int> GetSelectionLimitAsync(string userId, CancellationToken cancellationToken = default)
        => ClientCatalogLimits.Resolve(await context.Users.Where(x => x.Id == userId)
            .Select(x => x.ClientSelectionLimitOverride).SingleOrDefaultAsync(cancellationToken));

    public static void ValidateMultiSearch(int count, int selectionLimit)
    {
        if (count > selectionLimit)
        {
            throw new RequestValidationException($"Multi-search accepts at most {selectionLimit} unique domains for your account.");
        }
    }
}
