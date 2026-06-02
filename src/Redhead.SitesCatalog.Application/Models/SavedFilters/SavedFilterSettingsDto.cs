namespace Redhead.SitesCatalog.Application.Models.SavedFilters;

public sealed class SavedFilterSettingsDto
{
    public int SchemaVersion { get; init; }
    public List<string>? StopListDomains { get; init; }
    public string DrMin { get; init; } = string.Empty;
    public string DrMax { get; init; } = string.Empty;
    public string TrafficMin { get; init; } = string.Empty;
    public string TrafficMax { get; init; } = string.Empty;
    public string PriceMin { get; init; } = string.Empty;
    public string PriceMax { get; init; } = string.Empty;
    public List<SavedFilterLocationSelectionDto> LocationSelections { get; init; } = new();
    public List<string> ExcludedLocationKeys { get; init; } = new();
    public List<string> Niches { get; init; } = new();
    public List<string> CategorySearchTerms { get; init; } = new();
    public string TopicFitMode { get; init; } = string.Empty;
    public List<string> ExcludedNiches { get; init; } = new();
    public List<string> ExcludedCategorySearchTerms { get; init; } = new();
    public List<string> Languages { get; init; } = new();
    public List<string> CasinoAvailability { get; init; } = new();
    public List<string> CryptoAvailability { get; init; } = new();
    public List<string> LinkInsertAvailability { get; init; } = new();
    public List<string> LinkInsertCasinoAvailability { get; init; } = new();
    public List<string> DatingAvailability { get; init; } = new();
    public string Quarantine { get; init; } = string.Empty;
    public string? LastPublishedFromMonth { get; init; }
    public string? LastPublishedToMonth { get; init; }
}
