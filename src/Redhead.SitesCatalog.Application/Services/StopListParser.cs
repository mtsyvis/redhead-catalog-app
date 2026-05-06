using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Application.Services;

public static class StopListParser
{
    public static List<string>? Parse(IReadOnlyCollection<string>? rawDomains)
    {
        if (rawDomains is null || rawDomains.Count == 0)
        {
            return null;
        }

        var normalizedDomains = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rawDomain in rawDomains)
        {
            var normalizedDomain = DomainNormalizer.Normalize(rawDomain);
            if (!IsValidNormalizedDomain(normalizedDomain))
            {
                throw new RequestValidationException(
                    $"Invalid stop-list domain '{rawDomain}'. Enter a valid domain or URL.");
            }

            normalizedDomains.Add(normalizedDomain);
        }

        if (normalizedDomains.Count > StopListConstants.MaxDomains)
        {
            throw new RequestValidationException(
                $"Stop list accepts at most {StopListConstants.MaxDomains} unique domains. Received {normalizedDomains.Count}.");
        }

        return normalizedDomains.Count == 0
            ? null
            : normalizedDomains.OrderBy(domain => domain, StringComparer.Ordinal).ToList();
    }

    public static bool HasAnyInput(IReadOnlyCollection<string>? rawDomains)
        => rawDomains is { Count: > 0 };

    private static bool IsValidNormalizedDomain(string normalizedDomain)
    {
        if (string.IsNullOrWhiteSpace(normalizedDomain) || normalizedDomain.Length > 253)
        {
            return false;
        }

        if (!normalizedDomain.Contains('.', StringComparison.Ordinal) ||
            normalizedDomain.Any(char.IsWhiteSpace) ||
            normalizedDomain.Any(c => c is '/' or '?' or '#' or ':' or '@'))
        {
            return false;
        }

        var labels = normalizedDomain.Split('.');
        return labels.All(IsValidDomainLabel);
    }

    private static bool IsValidDomainLabel(string label)
    {
        if (label.Length is 0 or > 63 ||
            label[0] == '-' ||
            label[^1] == '-')
        {
            return false;
        }

        return label.All(c =>
            c is >= 'a' and <= 'z' ||
            c is >= '0' and <= '9' ||
            c == '-');
    }
}
