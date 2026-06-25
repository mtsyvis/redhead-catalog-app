using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

public interface ILiteMultiSearchUsageService
{
    Task<LiteMultiSearchUsageResult> TryConsumeAsync(
        string userId,
        int uniqueDomainCount,
        CancellationToken cancellationToken = default);
}
