using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain;

namespace Redhead.SitesCatalog.Application.Services.Import.Common;

internal static class ImportRowTrackingHelper
{
    public static void AddInvalidRow(
        InvalidRowsImportArtifactPayload payload,
        int sourceRowNumber,
        IReadOnlyCollection<string> rawValues,
        string errorMessage)
    {
        payload.Rows.Add(new InvalidImportRowRecord
        {
            SourceRowNumber = sourceRowNumber,
            RawValues = rawValues.ToList(),
            Errors = new List<string> { errorMessage }
        });
    }

    public static void TrackDuplicateDomain(
        string? rawDomain,
        IDictionary<string, int> occurrences,
        ICollection<string> duplicateDomainsInOrder)
    {
        var normalizedDomain = DomainNormalizer.Normalize(rawDomain);
        if (string.IsNullOrEmpty(normalizedDomain))
        {
            return;
        }

        if (occurrences.TryGetValue(normalizedDomain, out var count))
        {
            var nextCount = count + 1;
            occurrences[normalizedDomain] = nextCount;
            if (nextCount == 2)
            {
                duplicateDomainsInOrder.Add(normalizedDomain);
            }

            return;
        }

        occurrences[normalizedDomain] = 1;
    }
}
