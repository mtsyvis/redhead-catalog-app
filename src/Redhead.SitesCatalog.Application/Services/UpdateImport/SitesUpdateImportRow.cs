using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.UpdateImport;

internal sealed record SitesUpdatePriceOperation(
    string Header,
    PriceType PriceType,
    string TermKey,
    TermType? TermType,
    int? TermValue,
    TermUnit? TermUnit,
    string? RawValue,
    decimal? AmountUsd);

internal sealed record SitesUpdateAvailabilityOperation(
    string Header,
    PriceType ServiceType,
    string? RawValue,
    ServiceAvailabilityStatus Status);

internal sealed record SitesUpdateImportRow(string NormalizedDomain, IReadOnlySet<string> PresentColumns)
{
    public int SourceRowNumber { get; init; }
    public IReadOnlyList<string> RawValues { get; init; } = [];
    public double DR { get; init; }
    public long Traffic { get; init; }
    public string? Location { get; init; }
    public string? LocationKey { get; init; }
    public string? ImportedLocationRaw { get; init; }
    public string? LocationWarningDetails { get; init; }
    public IReadOnlyList<SitesUpdatePriceOperation> PriceOperations { get; init; } = [];
    public IReadOnlyList<SitesUpdateAvailabilityOperation> AvailabilityOperations { get; init; } = [];
    public int? NumberDFLinks { get; init; }
    public string? Language { get; init; }
    public string? Niche { get; init; }
    public string? Categories { get; init; }
    public string? SponsoredTag { get; init; }
}
