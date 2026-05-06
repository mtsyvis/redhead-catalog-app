using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Application.Services;

public sealed class NicheFilterOptionsCache : INicheFilterOptionsCache
{
    private const string CacheKey = "sites:niche-filter-options:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(60);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public NicheFilterOptionsCache(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<FilterOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<List<FilterOptionDto>>(CacheKey, out var cached) && cached is not null)
        {
            return Copy(cached);
        }

        var options = await LoadOptionsAsync(cancellationToken);
        _cache.Set(CacheKey, options, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        return Copy(options);
    }

    public void Invalidate()
    {
        _cache.Remove(CacheKey);
    }

    private async Task<List<FilterOptionDto>> LoadOptionsAsync(CancellationToken cancellationToken)
    {
        // Kept an EF InMemory fallback for tests.
        if (IsInMemoryProvider())
        {
            return await LoadOptionsWithInMemoryProviderAsync(cancellationToken);
        }

        return await _context.Database.SqlQueryRaw<FilterOptionDto>(
                """
                SELECT token AS "Value", initcap(token) AS "Label"
                FROM (
                    SELECT DISTINCT unnest("NicheTokens") AS token
                    FROM "Sites"
                ) AS tokens
                WHERE token <> ''
                ORDER BY initcap(token), token
                """)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<FilterOptionDto>> LoadOptionsWithInMemoryProviderAsync(CancellationToken cancellationToken)
    {
        var tokenArrays = await _context.Sites
            .AsNoTracking()
            .Select(s => s.NicheTokens)
            .ToListAsync(cancellationToken);

        return tokenArrays
            .SelectMany(tokens => tokens)
            .Where(token => token != string.Empty)
            .Distinct(StringComparer.Ordinal)
            .Select(token => new FilterOptionDto
            {
                Value = token,
                Label = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(token)
            })
            .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(option => option.Value, StringComparer.Ordinal)
            .ToList();
    }

    private bool IsInMemoryProvider()
    {
        return string.Equals(
            _context.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);
    }

    private static List<FilterOptionDto> Copy(IEnumerable<FilterOptionDto> source)
    {
        return source
            .Select(option => new FilterOptionDto
            {
                Value = option.Value,
                Label = option.Label
            })
            .ToList();
    }
}
