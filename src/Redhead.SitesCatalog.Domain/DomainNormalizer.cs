namespace Redhead.SitesCatalog.Domain;

/// <summary>
/// Provides domain normalization functionality for consistent domain handling.
/// </summary>
public static class DomainNormalizer
{
    /// <summary>
    /// Normalizes a domain/URL to a consistent format.
    /// Rules:
    /// - Trim whitespace
    /// - Remove scheme (http://, https://)
    /// - Remove leading www.
    /// - Remove trailing slash
    /// - Keep only host (strip path/query/fragment)
    /// - Lowercase
    /// </summary>
    /// <param name="input">The domain or URL to normalize</param>
    /// <returns>Normalized domain or empty string if input is null/empty</returns>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Trim whitespace
        var result = input.Trim();

        // Remove scheme if present
        if (result.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(7);
        }
        else if (result.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(8);
        }

        // If there's a path, query, or fragment, keep only the host part
        var firstSlashIndex = result.IndexOf('/');
        if (firstSlashIndex >= 0)
        {
            result = result.Substring(0, firstSlashIndex);
        }

        var firstQuestionIndex = result.IndexOf('?');
        if (firstQuestionIndex >= 0)
        {
            result = result.Substring(0, firstQuestionIndex);
        }

        var firstHashIndex = result.IndexOf('#');
        if (firstHashIndex >= 0)
        {
            result = result.Substring(0, firstHashIndex);
        }

        // Remove leading www.
        if (result.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            result = result.Substring(4);
        }

        // Remove trailing slash (should already be removed by path handling, but just in case)
        result = result.TrimEnd('/');

        // Lowercase
        result = result.ToLowerInvariant();

        return result;
    }
}
