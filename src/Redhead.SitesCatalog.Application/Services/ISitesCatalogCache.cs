using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

public interface ISitesCatalogCache
{
    Task<List<FilterOptionDto>> GetNicheOptionsAsync(
        Func<CancellationToken, Task<List<FilterOptionDto>>> load,
        CancellationToken cancellationToken = default);

    Task<LocationFilterOptionsDto> GetLocationOptionsAsync(
        Func<CancellationToken, Task<LocationFilterOptionsDto>> load,
        CancellationToken cancellationToken = default);

    Task<List<TermFilterOptionDto>> GetTermOptionsAsync(
        Func<CancellationToken, Task<List<TermFilterOptionDto>>> load,
        CancellationToken cancellationToken = default);

    void InvalidateNicheOptions();

    void InvalidateTermOptions();

    void InvalidateLocationOptions();
}
