using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services;

public static class CategorySearchTermParser
{
    public static List<string>? NormalizeAndValidate(IReadOnlyCollection<string?>? terms)
    {
        if (terms is null || terms.Count == 0)
        {
            return null;
        }

        var normalizedTerms = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                continue;
            }

            var trimmed = term.Trim();
            if (trimmed.Length > CategorySearchConstants.MaxTermLength)
            {
                throw new RequestValidationException(
                    $"Category search terms must be at most {CategorySearchConstants.MaxTermLength} characters each.");
            }

            if (seen.Add(trimmed))
            {
                normalizedTerms.Add(trimmed);
            }
        }

        if (normalizedTerms.Count > CategorySearchConstants.MaxTerms)
        {
            throw new RequestValidationException(
                $"Category search supports at most {CategorySearchConstants.MaxTerms} unique terms.");
        }

        return normalizedTerms.Count == 0 ? null : normalizedTerms;
    }

    public static string EscapeLikeTerm(string term)
        => term
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
