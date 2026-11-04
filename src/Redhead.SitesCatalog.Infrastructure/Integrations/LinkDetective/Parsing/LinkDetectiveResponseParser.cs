using System.Text.Json;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Parsing;

// Keeps response validation and partial-result handling independent of HTTP requests.
internal static class LinkDetectiveResponseParser
{
    private const string DomainPropertyName = "domain";

    public static LinkDetectivePricesResponse ParseSingleDomain(LinkDetectiveApiResponse response, string domain, DateTime now)
    {
        var batch = ParsePrices(response, [domain], now, rejectMalformedRows: true);
        if (batch.IsTruncated || !batch.PricesByDomain.TryGetValue(domain, out var prices))
        {
            throw LinkDetectiveApiException.IncompleteResponse();
        }

        return new LinkDetectivePricesResponse(prices, batch.Quota);
    }

    public static LinkDetectiveBatchPricesResponse ParseBatch(
        LinkDetectiveApiResponse response, IReadOnlyCollection<string> requestedDomains, DateTime now) =>
        ParsePrices(response, requestedDomains, now, rejectMalformedRows: false);

    private static LinkDetectiveBatchPricesResponse ParsePrices(
        LinkDetectiveApiResponse response, IReadOnlyCollection<string> requestedDomains, DateTime now, bool rejectMalformedRows)
    {
        if (response.Results.ValueKind != JsonValueKind.Array)
        {
            throw LinkDetectiveApiException.IncompleteResponse();
        }

        var results = new Dictionary<string, LinkDetectivePrices>(StringComparer.Ordinal);
        var priceRowsByDomain = ReadPriceRows(response.Results, rejectMalformedRows)
            .ToLookup(entry => entry.Domain, entry => entry.Price, StringComparer.Ordinal);
        var notFoundDomains = ReadNotFoundDomains(response.NotFound, rejectMalformedRows)
            .ToLookup(domain => domain, StringComparer.Ordinal);
        foreach (var domain in requestedDomains)
        {
            var prices = ReadConfirmedPrices(priceRowsByDomain[domain].ToArray(), notFoundDomains[domain].Count());
            if (prices is not null)
            {
                results[domain] = prices;
            }
        }

        var quota = LinkDetectiveQuotaParser.Parse(response.Quota, now, rejectMalformedRows);
        return new LinkDetectiveBatchPricesResponse(results, response.Truncated, quota);
    }

    private static LinkDetectivePrices? ReadConfirmedPrices(PriceRow?[] rows, int notFoundCount)
    {
        if (rows.Length == 0)
        {
            return notFoundCount == 1 ? new(null, null, null) : null;
        }

        // Duplicated or contradictory results remain unresolved and preserve saved prices.
        if (rows.Length != 1 || notFoundCount > 0)
        {
            return null;
        }

        var row = rows[0];
        if (row is null || row.Price is not >= 0 || row.Avg is not >= 0 || row.Median is not >= 0)
        {
            return null;
        }

        return new(row.Price, row.Avg, row.Median);
    }

    private static IEnumerable<(string Domain, PriceRow? Price)> ReadPriceRows(JsonElement rows, bool rejectMalformedRows)
    {
        foreach (var element in rows.EnumerateArray())
        {
            var price = LinkDetectiveJsonReader.TryRead<PriceRow>(element);
            if (rejectMalformedRows && price is null)
            {
                throw LinkDetectiveApiException.IncompleteResponse();
            }

            // Keep a malformed row's domain in the lookup: a malformed duplicate makes that
            // domain ambiguous instead of allowing another row to become its winning price.
            var domain = DomainNormalizer.Normalize(price?.Domain ?? ReadRawDomain(element));
            if (domain is not null)
            {
                yield return (domain, price);
            }
        }
    }

    private static string? ReadRawDomain(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? domain = null;
        foreach (var property in row.EnumerateObject())
        {
            if (property.Name.Equals(DomainPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                domain = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            }
        }

        return domain;
    }

    private static IEnumerable<string> ReadNotFoundDomains(JsonElement values, bool rejectMalformedRows)
    {
        if (values.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            yield break;
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            if (rejectMalformedRows)
            {
                throw LinkDetectiveApiException.IncompleteResponse();
            }

            yield break;
        }

        foreach (var element in values.EnumerateArray())
        {
            if (rejectMalformedRows && element.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            {
                throw LinkDetectiveApiException.IncompleteResponse();
            }

            var domain = element.ValueKind == JsonValueKind.String ? DomainNormalizer.Normalize(element.GetString()) : null;
            if (domain is not null)
            {
                yield return domain;
            }
        }
    }

    private sealed record PriceRow(string? Domain, decimal? Price, decimal? Avg, decimal? Median);
}
