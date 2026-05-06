using System.Text.RegularExpressions;

namespace Redhead.SitesCatalog.Domain;

/// <summary>
/// Normalizes free-form Niche values into filterable tokens.
/// </summary>
public static partial class NicheNormalizer
{
    private static readonly HashSet<string> EmptyMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "n/a",
        "na",
        "-",
        "none",
        "null"
    };

    public static string[] NormalizeTokens(string? input)
    {
        return NormalizeTokens(input is null ? [] : [input]);
    }

    public static string[] NormalizeTokens(IEnumerable<string?> inputs)
    {
        var tokens = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var input in inputs)
        {
            AddTokens(input, tokens, seen);
        }

        return tokens.ToArray();
    }

    private static void AddTokens(string? input, List<string> tokens, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        foreach (var part in input.Split(','))
        {
            var token = WhitespaceRegex()
                .Replace(part.Trim(), " ")
                .ToLowerInvariant();

            if (string.IsNullOrEmpty(token) || EmptyMarkers.Contains(token))
            {
                continue;
            }

            if (seen.Add(token))
            {
                tokens.Add(token);
            }
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
