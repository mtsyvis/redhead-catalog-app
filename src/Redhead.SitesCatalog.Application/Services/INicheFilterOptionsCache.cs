using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

public interface INicheFilterOptionsCache
{
    Task<List<FilterOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default);

    void Invalidate();
}
