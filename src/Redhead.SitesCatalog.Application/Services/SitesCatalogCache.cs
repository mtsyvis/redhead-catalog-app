using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Application.Models;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class SitesCatalogCache : ISitesCatalogCache
{
    private const string NicheOptionsCacheKey = "sites:catalog:niche-options:v1";
    private const string LocationOptionsCacheKey = "sites:catalog:location-options:v1";
    private const string TermOptionsCacheKey = "sites:catalog:term-options:v1";
    private static readonly TimeSpan FilterOptionsCacheDuration = TimeSpan.FromMinutes(60);

    private readonly IMemoryCache _cache;

    public SitesCatalogCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<List<FilterOptionDto>> GetNicheOptionsAsync(
        Func<CancellationToken, Task<List<FilterOptionDto>>> load,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<List<FilterOptionDto>>(NicheOptionsCacheKey, out var cached) &&
            cached is not null)
        {
            return Copy(cached);
        }

        var result = await load(cancellationToken);
        _cache.Set(NicheOptionsCacheKey, Copy(result), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = FilterOptionsCacheDuration
        });

        return Copy(result);
    }

    public async Task<LocationFilterOptionsDto> GetLocationOptionsAsync(
        Func<CancellationToken, Task<LocationFilterOptionsDto>> load,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<LocationFilterOptionsDto>(LocationOptionsCacheKey, out var cached) &&
            cached is not null)
        {
            return Copy(cached);
        }

        var result = await load(cancellationToken);
        _cache.Set(LocationOptionsCacheKey, Copy(result), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = FilterOptionsCacheDuration
        });

        return Copy(result);
    }

    public async Task<List<TermFilterOptionDto>> GetTermOptionsAsync(
        Func<CancellationToken, Task<List<TermFilterOptionDto>>> load,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<List<TermFilterOptionDto>>(TermOptionsCacheKey, out var cached) &&
            cached is not null)
        {
            return Copy(cached);
        }

        var result = await load(cancellationToken);
        _cache.Set(TermOptionsCacheKey, Copy(result), new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = FilterOptionsCacheDuration
        });

        return Copy(result);
    }

    public void InvalidateNicheOptions()
    {
        _cache.Remove(NicheOptionsCacheKey);
    }

    public void InvalidateTermOptions()
    {
        _cache.Remove(TermOptionsCacheKey);
    }

    public void InvalidateLocationOptions()
    {
        _cache.Remove(LocationOptionsCacheKey);
    }

    private static List<FilterOptionDto> Copy(IEnumerable<FilterOptionDto> source)
        => source.Select(option => new FilterOptionDto
        {
            Value = option.Value,
            Label = option.Label
        }).ToList();

    private static List<TermFilterOptionDto> Copy(IEnumerable<TermFilterOptionDto> source)
        => source.Select(term => new TermFilterOptionDto
        {
            TermKey = term.TermKey,
            Label = term.Label,
            TermType = term.TermType,
            TermValue = term.TermValue,
            TermUnit = term.TermUnit
        }).ToList();

    private static LocationFilterOptionsDto Copy(LocationFilterOptionsDto source)
    {
        return new LocationFilterOptionsDto
        {
            Groups = source.Groups.Select(group => new LocationGroupFilterOptionDto
            {
                Key = group.Key,
                DisplayName = group.DisplayName,
                GroupType = group.GroupType,
                LocationCount = group.LocationCount,
                Locations = group.Locations.Select(Copy).ToList()
            }).ToList(),
            Locations = source.Locations.Select(Copy).ToList(),
            Special = new LocationSpecialFilterOptionsDto
            {
                Unknown = Copy(source.Special.Unknown),
                Other = source.Special.Other is null ? null : Copy(source.Special.Other)
            }
        };
    }

    private static LocationFilterOptionDto Copy(LocationFilterOptionDto source)
    {
        return new LocationFilterOptionDto
        {
            Key = source.Key,
            DisplayName = source.DisplayName
        };
    }
}
