using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Parses multi-search query text into normalized unique domains and duplicate list.
/// </summary>
public static class MultiSearchParser
{
    private static readonly char[] Whitespace = [' ', '\t', '\n', '\r'];

    /// <summary>
    /// Splits query text by whitespace, normalizes each input, enforces max input count,
    /// and returns unique domains to search plus the list of duplicated domains.
    /// </summary>
    /// <param name="queryText">Raw input (domains/URLs separated by whitespace)</param>
    /// <returns>Unique domains to search and list of domains that appeared more than once</returns>
    /// <exception cref="RequestValidationException">Thrown when input count exceeds MaxInputs (500)</exception>
    public static MultiSearchParseResult Parse(string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return new MultiSearchParseResult
            {
                UniqueDomains = [],
                Duplicates = []
            };
        }

        var rawTokens = queryText!
            .Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);

        if (rawTokens.Length > MultiSearchConstants.MaxInputs)
        {
            throw new RequestValidationException(
                $"Multi-search accepts at most {MultiSearchConstants.MaxInputs} inputs. Received {rawTokens.Length}.");
        }

        var normalized = rawTokens
            .Select(DomainNormalizer.Normalize)
            .Where(s => s.Length > 0)
            .ToList();

        var duplicates = normalized
            .GroupBy(x => x, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var uniqueDomains = normalized
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new MultiSearchParseResult
        {
            UniqueDomains = uniqueDomains,
            Duplicates = duplicates
        };
    }
}
