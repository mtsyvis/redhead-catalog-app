namespace Redhead.SitesCatalog.Infrastructure.Locations;

public sealed record LocationNormalizationResult(
    LocationNormalizationStatus Status,
    string? LocationKey,
    string? RawValue)
{
    public bool IsMapped => Status is LocationNormalizationStatus.Known or LocationNormalizationStatus.Unknown;
}

public enum LocationNormalizationStatus
{
    Known,
    Unknown,
    Unmapped
}
