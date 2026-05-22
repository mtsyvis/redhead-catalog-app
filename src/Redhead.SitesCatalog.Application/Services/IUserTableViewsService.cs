using Redhead.SitesCatalog.Application.Models.TableViews;

namespace Redhead.SitesCatalog.Application.Services;

public interface IUserTableViewsService
{
    Task<TableViewsResponseDto> GetTableViewsAsync(
        string userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task SetActiveViewAsync(
        string userId,
        string tableKey,
        string viewType,
        string viewKey,
        CancellationToken cancellationToken);

    Task<TableCustomViewDto> CreateCustomViewAsync(
        string userId,
        string tableKey,
        string name,
        TableViewSettingsDto settings,
        CancellationToken cancellationToken);

    Task<TableCustomViewDto> UpdateCustomViewAsync(
        string userId,
        string tableKey,
        Guid id,
        string? name,
        TableViewSettingsDto? settings,
        CancellationToken cancellationToken);

    Task DeleteCustomViewAsync(
        string userId,
        string tableKey,
        Guid id,
        CancellationToken cancellationToken);
}
