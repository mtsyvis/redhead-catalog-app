using Redhead.SitesCatalog.Application.Models.WebmasterSearch;

namespace Redhead.SitesCatalog.Application.Services.WebmasterSearch;

public interface IWebmasterSearchService
{
    Task<WebmasterSearchResult> SearchAsync(
        string? contact,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<WebmasterWorkspaceDto?> GetWorkspaceAsync(
        Guid webmasterId,
        CancellationToken cancellationToken = default);
}
