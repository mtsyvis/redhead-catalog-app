using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;

public sealed class LinkDetectiveApiClient(
    IHttpClientFactory clients,
    IOptions<LinkDetectiveOptions> options,
    LinkDetectiveRequestCoordinator coordinator,
    TimeProvider clock) : ILinkDetectiveApiClient
{
    public const string HttpClientName = "LinkDetective";

    public async Task<LinkDetectiveDomainPrices> GetPricesAsync(string domain, CancellationToken cancellationToken)
    {
        using var slot = await coordinator.BeginRequestAsync(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://linkdetective.pro/api/v2/domains");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiToken?.Trim());
            request.Content = JsonContent.Create(new { domains = new[] { domain } });
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var retryAt = response.Headers.RetryAfter?.Date?.UtcDateTime
                    ?? now.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
                retryAt = retryAt > now ? retryAt : now.AddMinutes(1);
                coordinator.BlockUntil(retryAt);
                throw new LinkDetectiveApiException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "LinkDetective access is unavailable. Contact an administrator.",
                    HttpStatusCode.TooManyRequests => "LinkDetective request limit or quota reached. Try again later.",
                    _ => "LinkDetective could not update prices. Try again later."
                }, retryAt);
            }

            // Only price aggregates are deserialized; contacts, sellers, and metrics are ignored.
            var payload = await response.Content.ReadFromJsonAsync<DomainResponse>(cancellationToken);
            if (payload is null || payload.Truncated || payload.Results is null || payload.Results.Any(row => row is null))
            {
                throw InvalidResponse();
            }

            if (payload.Quota?.Remaining == 0)
            {
                var now = clock.GetUtcNow().UtcDateTime;
                var resetAt = payload.Quota.ResetAt?.UtcDateTime;
                coordinator.BlockUntil(resetAt > now ? resetAt.Value
                    : payload.Quota.Period == "day" ? now.Date.AddDays(1) : now.AddMinutes(5));
            }

            var rows = payload.Results.OfType<PriceRow>().Where(row => DomainNormalizer.Normalize(row.Domain) == domain).ToList();
            if (rows.Count == 1)
            {
                var row = rows[0];
                if (row.Price is not >= 0 || row.Avg is not >= 0 || row.Median is not >= 0)
                {
                    throw InvalidResponse();
                }
                return new LinkDetectiveDomainPrices(row.Price, row.Avg, row.Median);
            }
            if (rows.Count == 0 && payload.NotFound?.Any(value => DomainNormalizer.Normalize(value) == domain) == true)
            {
                return new LinkDetectiveDomainPrices(null, null, null);
            }

            throw InvalidResponse();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // Never propagate an upstream response, token, or request details to logs/UI.
            throw new LinkDetectiveApiException("LinkDetective could not update prices. Try again later.");
        }
    }

    private static LinkDetectiveApiException InvalidResponse()
        => new("LinkDetective returned an incomplete price response. Saved prices have been kept.");

    private sealed record DomainResponse(List<PriceRow?>? Results, List<string?>? NotFound, bool Truncated, QuotaRow? Quota);
    private sealed record PriceRow(string? Domain, decimal? Price, decimal? Avg, decimal? Median);
    private sealed record QuotaRow(long? Remaining, string? Period, DateTimeOffset? ResetAt);
}
