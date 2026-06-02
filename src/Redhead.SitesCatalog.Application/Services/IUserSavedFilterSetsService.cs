using Redhead.SitesCatalog.Application.Models.SavedFilters;

namespace Redhead.SitesCatalog.Application.Services;

public interface IUserSavedFilterSetsService
{
    Task<SavedFilterSetsResponseDto> GetFilterSetsAsync(
        string userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task<SavedFilterSetDto> CreateFilterSetAsync(
        string userId,
        string tableKey,
        string name,
        SavedFilterSettingsDto settings,
        CancellationToken cancellationToken);

    Task<SavedFilterSetDto> UpdateFilterSetAsync(
        string userId,
        string tableKey,
        Guid id,
        string? name,
        SavedFilterSettingsDto? settings,
        CancellationToken cancellationToken);

    Task DeleteFilterSetAsync(
        string userId,
        string tableKey,
        Guid id,
        CancellationToken cancellationToken);
}
