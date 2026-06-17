using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class SitesCatalogCacheTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly SitesCatalogCache _cache;

    public SitesCatalogCacheTests()
    {
        _cache = new SitesCatalogCache(_memoryCache);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    [Fact]
    public async Task GetFilterOptionsAsync_CachesEachOptionGroupAndReturnsCopies()
    {
        // Arrange
        var nicheCalls = 0;
        var locationCalls = 0;
        var termCalls = 0;

        // Act
        var firstNiches = await _cache.GetNicheOptionsAsync(_ =>
        {
            nicheCalls++;
            return Task.FromResult(CreateNicheOptions("tech"));
        });
        var firstLocations = await _cache.GetLocationOptionsAsync(_ =>
        {
            locationCalls++;
            return Task.FromResult(CreateLocationOptions());
        });
        var firstTerms = await _cache.GetTermOptionsAsync(_ =>
        {
            termCalls++;
            return Task.FromResult(CreateTermOptions("finite:1:year"));
        });
        firstNiches[0].Value = "mutated";
        firstLocations.Groups[0].Locations[0].DisplayName = "Mutated";

        var secondNiches = await _cache.GetNicheOptionsAsync(_ =>
        {
            nicheCalls++;
            return Task.FromResult(CreateNicheOptions("finance"));
        });
        var secondLocations = await _cache.GetLocationOptionsAsync(_ =>
        {
            locationCalls++;
            return Task.FromResult(new LocationFilterOptionsDto());
        });
        var secondTerms = await _cache.GetTermOptionsAsync(_ =>
        {
            termCalls++;
            return Task.FromResult(CreateTermOptions("permanent"));
        });

        // Assert
        Assert.Equal(1, nicheCalls);
        Assert.Equal(1, locationCalls);
        Assert.Equal(1, termCalls);
        Assert.Equal("tech", secondNiches[0].Value);
        Assert.Equal("United States", secondLocations.Groups[0].Locations[0].DisplayName);
        Assert.Equal("finite:1:year", secondTerms[0].TermKey);
    }

    [Fact]
    public async Task GranularInvalidation_ClearsOnlyRequestedCacheGroup()
    {
        // Arrange
        var nicheCalls = 0;
        var locationCalls = 0;
        var termCalls = 0;
        await _cache.GetNicheOptionsAsync(_ =>
        {
            nicheCalls++;
            return Task.FromResult(CreateNicheOptions("tech"));
        });
        await _cache.GetLocationOptionsAsync(_ =>
        {
            locationCalls++;
            return Task.FromResult(CreateLocationOptions());
        });
        await _cache.GetTermOptionsAsync(_ =>
        {
            termCalls++;
            return Task.FromResult(CreateTermOptions("finite:1:year"));
        });

        // Act
        _cache.InvalidateNicheOptions();
        await _cache.GetNicheOptionsAsync(_ =>
        {
            nicheCalls++;
            return Task.FromResult(CreateNicheOptions("finance"));
        });
        await _cache.GetLocationOptionsAsync(_ =>
        {
            locationCalls++;
            return Task.FromResult(new LocationFilterOptionsDto());
        });
        await _cache.GetTermOptionsAsync(_ =>
        {
            termCalls++;
            return Task.FromResult(CreateTermOptions("permanent"));
        });

        // Assert
        Assert.Equal(2, nicheCalls);
        Assert.Equal(1, locationCalls);
        Assert.Equal(1, termCalls);
    }

    private static List<FilterOptionDto> CreateNicheOptions(string niche)
    {
        return
        [
            new FilterOptionDto
            {
                Value = niche,
                Label = niche
            }
        ];
    }

    private static LocationFilterOptionsDto CreateLocationOptions()
    {
        return new LocationFilterOptionsDto
        {
            Groups =
            [
                new LocationGroupFilterOptionDto
                {
                    Key = "north-america",
                    DisplayName = "North America",
                    GroupType = "region",
                    LocationCount = 1,
                    Locations =
                    [
                        new LocationFilterOptionDto
                        {
                            Key = "US",
                            DisplayName = "United States"
                        }
                    ]
                }
            ],
            Locations =
            [
                new LocationFilterOptionDto
                {
                    Key = "US",
                    DisplayName = "United States"
                }
            ],
            Special = new LocationSpecialFilterOptionsDto
            {
                Unknown = new LocationFilterOptionDto
                {
                    Key = LocationConstants.UnknownLocationKey,
                    DisplayName = "Unknown"
                }
            }
        };
    }

    private static List<TermFilterOptionDto> CreateTermOptions(string termKey)
    {
        return
        [
            new TermFilterOptionDto
            {
                TermKey = termKey,
                Label = termKey,
                TermType = termKey == "permanent" ? TermType.Permanent : TermType.Finite,
                TermValue = termKey == "permanent" ? null : 1,
                TermUnit = termKey == "permanent" ? null : TermUnit.Year
            }
        ];
    }
}
