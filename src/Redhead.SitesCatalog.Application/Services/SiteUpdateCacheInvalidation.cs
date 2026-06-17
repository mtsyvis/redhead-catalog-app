using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services;

internal static class SiteUpdateCacheInvalidation
{
    public static void InvalidateAfterSiteUpdate(
        ISitesCatalogCache cache,
        SiteUpdateCacheSnapshot previous,
        SiteUpdateCacheSnapshot next)
    {
        if (previous.HasDifferentNicheOptionsThan(next))
        {
            cache.InvalidateNicheOptions();
        }

        if (previous.HasDifferentLocationOptionsThan(next))
        {
            cache.InvalidateLocationOptions();
        }

        if (previous.HasDifferentTermOptionsThan(next))
        {
            cache.InvalidateTermOptions();
        }
    }
}

internal sealed record SiteUpdateCacheSnapshot(
    string[] NicheTokens,
    bool LocationWasOther,
    HashSet<SiteUpdateTermDescriptor>? TermDescriptors)
{
    public static SiteUpdateCacheSnapshot FromSite(Site site)
    {
        return new SiteUpdateCacheSnapshot(
            site.NicheTokens.ToArray(),
            site.LocationKey is null,
            GetTermDescriptors(site.PriceOptions));
    }

    public static SiteUpdateCacheSnapshot FromUpdate(
        UpdateSiteRequest request,
        string? locationKey,
        string[] nicheTokens)
    {
        return new SiteUpdateCacheSnapshot(
            nicheTokens,
            locationKey is null,
            request.Pricing is null ? null : GetTermDescriptors(request.Pricing.Prices));
    }

    public bool HasDifferentNicheOptionsThan(SiteUpdateCacheSnapshot next)
    {
        return !NicheTokens.ToHashSet(StringComparer.Ordinal).SetEquals(next.NicheTokens);
    }

    public bool HasDifferentLocationOptionsThan(SiteUpdateCacheSnapshot next)
    {
        return LocationWasOther != next.LocationWasOther;
    }

    public bool HasDifferentTermOptionsThan(SiteUpdateCacheSnapshot next)
    {
        return next.TermDescriptors is not null
            && TermDescriptors is not null
            && !TermDescriptors.SetEquals(next.TermDescriptors);
    }

    private static HashSet<SiteUpdateTermDescriptor> GetTermDescriptors(IEnumerable<SitePriceOption> prices)
    {
        return prices
            .Where(price => !string.IsNullOrWhiteSpace(price.TermKey))
            .Select(price => new SiteUpdateTermDescriptor(
                price.TermKey,
                price.TermType,
                price.TermValue,
                price.TermUnit))
            .ToHashSet();
    }

    private static HashSet<SiteUpdateTermDescriptor> GetTermDescriptors(IEnumerable<UpdateSitePriceOptionRequest> prices)
    {
        return prices
            .Where(price => !string.IsNullOrWhiteSpace(price.TermKey))
            .Select(price => new SiteUpdateTermDescriptor(
                price.TermKey,
                price.TermType,
                price.TermValue,
                price.TermUnit))
            .ToHashSet();
    }
}

internal readonly record struct SiteUpdateTermDescriptor(
    string TermKey,
    TermType? TermType,
    int? TermValue,
    TermUnit? TermUnit);
