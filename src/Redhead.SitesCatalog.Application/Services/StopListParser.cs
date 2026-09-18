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
            if (string.IsNullOrWhiteSpace(rawDomain))
            {
                continue;
            }

            var normalizedDomain = DomainNormalizer.Normalize(rawDomain);
            if (!DomainValidator.IsValidNormalizedDomain(normalizedDomain))
            {
                throw new RequestValidationException(
                    $"Invalid stop-list domain '{rawDomain}'. Enter a valid domain or URL.");
            }

            normalizedDomains.Add(normalizedDomain);
        }

        if (normalizedDomains.Count > StopListConstants.MaxStopListDomains)
        {
            throw new RequestValidationException(
                $"Stop list accepts at most {StopListConstants.MaxStopListDomains} unique domains. Received {normalizedDomains.Count}.");
        }

        return normalizedDomains.Count == 0
            ? null
            : normalizedDomains.OrderBy(domain => domain, StringComparer.Ordinal).ToList();
    }

    public static bool HasAnyInput(IReadOnlyCollection<string>? rawDomains)
        => rawDomains?.Any(domain => !string.IsNullOrWhiteSpace(domain)) == true;
}
