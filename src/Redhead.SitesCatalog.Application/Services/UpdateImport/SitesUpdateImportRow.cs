using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.UpdateImport;

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
    public decimal? PriceUsd { get; init; }
    public decimal? PriceCasino { get; init; }
    public ServiceAvailabilityStatus PriceCasinoStatus { get; init; }
    public decimal? PriceCrypto { get; init; }
    public ServiceAvailabilityStatus PriceCryptoStatus { get; init; }
    public decimal? PriceLinkInsert { get; init; }
    public ServiceAvailabilityStatus PriceLinkInsertStatus { get; init; }
    public decimal? PriceLinkInsertCasino { get; init; }
    public ServiceAvailabilityStatus PriceLinkInsertCasinoStatus { get; init; }
    public decimal? PriceDating { get; init; }
    public ServiceAvailabilityStatus PriceDatingStatus { get; init; }
    public int? NumberDFLinks { get; init; }
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
    public string? Language { get; init; }
    public string? Niche { get; init; }
    public string? Categories { get; init; }
    public string? SponsoredTag { get; init; }
}
