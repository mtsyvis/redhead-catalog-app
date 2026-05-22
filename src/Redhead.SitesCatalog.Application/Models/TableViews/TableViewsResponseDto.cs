namespace Redhead.SitesCatalog.Application.Models.TableViews;

public sealed record TableViewsResponseDto(
    string ActiveViewType,
    string ActiveViewKey,
    IReadOnlyList<TableCustomViewDto> CustomViews);
