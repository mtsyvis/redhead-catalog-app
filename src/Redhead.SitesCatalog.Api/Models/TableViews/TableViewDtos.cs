using Redhead.SitesCatalog.Application.Models.TableViews;

namespace Redhead.SitesCatalog.Api.Models.TableViews;

public sealed record SetActiveTableViewRequest(string ViewType, string ViewKey);

public sealed record CreateTableCustomViewRequest(
    string Name,
    TableViewSettingsDto Settings);

public sealed record UpdateTableCustomViewRequest(
    string? Name,
    TableViewSettingsDto? Settings);
