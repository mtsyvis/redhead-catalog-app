namespace Redhead.SitesCatalog.Application.Models.SavedFilters;

public sealed class SavedFilterLocationSelectionDto
{
    public string Kind { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? GroupType { get; init; }
    public int? LocationCount { get; init; }
    public string? LocationKey { get; init; }
    public List<SavedFilterLocationOptionDto> Locations { get; init; } = new();
}

public sealed class SavedFilterLocationOptionDto
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
