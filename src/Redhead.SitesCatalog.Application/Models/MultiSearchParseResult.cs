namespace Redhead.SitesCatalog.Application.Models;

/// <summary>
/// Result of parsing multi-search query text: unique domains to search and list of duplicated domains
/// </summary>
public class MultiSearchParseResult
{
    /// <summary>
    /// Unique normalized domains to look up (duplicates removed)
    /// </summary>
    public IReadOnlyList<string> UniqueDomains { get; init; } = [];

    /// <summary>
    /// Normalized domains that appeared more than once in the input
    /// </summary>
    public IReadOnlyList<string> Duplicates { get; init; } = [];
}
