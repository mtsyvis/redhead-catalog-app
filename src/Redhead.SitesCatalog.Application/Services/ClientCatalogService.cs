using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public interface IClientCatalogService
{
    Task<int> GetSelectionLimitAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class ClientCatalogService(ApplicationDbContext context) : IClientCatalogService
{
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
