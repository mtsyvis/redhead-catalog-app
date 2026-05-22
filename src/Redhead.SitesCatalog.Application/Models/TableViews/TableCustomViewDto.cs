namespace Redhead.SitesCatalog.Application.Models.TableViews;

public sealed record TableCustomViewDto(
    Guid Id,
    string Name,
    int SchemaVersion,
    TableViewSettingsDto Settings,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
