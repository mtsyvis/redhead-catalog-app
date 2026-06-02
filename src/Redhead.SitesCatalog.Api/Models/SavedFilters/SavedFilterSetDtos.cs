using Redhead.SitesCatalog.Application.Models.SavedFilters;

namespace Redhead.SitesCatalog.Api.Models.SavedFilters;

public sealed record CreateSavedFilterSetRequest(
    string Name,
    SavedFilterSettingsDto Settings);

public sealed record UpdateSavedFilterSetRequest(
    string? Name,
    SavedFilterSettingsDto? Settings);
