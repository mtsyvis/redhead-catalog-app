namespace Redhead.SitesCatalog.Infrastructure.Locations;

public interface ILocationNormalizer
{
    LocationNormalizationResult Normalize(string? value);
}
