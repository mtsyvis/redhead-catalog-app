namespace Redhead.SitesCatalog.Application.Models.SavedFilters;

public sealed record SavedFilterSetDto(
    Guid Id,
    string Name,
    int SchemaVersion,
    SavedFilterSettingsDto Settings,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
