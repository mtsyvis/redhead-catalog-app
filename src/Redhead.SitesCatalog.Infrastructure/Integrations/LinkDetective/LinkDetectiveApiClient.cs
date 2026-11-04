using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Constants;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Parsing;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;

public sealed class LinkDetectiveApiClient(
    IHttpClientFactory clients,
    IOptions<LinkDetectiveOptions> options,
    LinkDetectiveRequestCoordinator coordinator,
    TimeProvider clock) : ILinkDetectiveApiClient
{
    public const string HttpClientName = "LinkDetective";
    private const string ApiBaseUrl = "https://linkdetective.pro/api/v2/";
    private const string DomainsEndpoint = "domains";
    private const string QuotaEndpoint = "inventory/preview";
    private static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMinutes(1);

    public async Task<LinkDetectivePricesResponse> GetPricesAsync(string domain, CancellationToken cancellationToken)
    {
        var requestedDomains = NormalizeDomains([domain]);
        var response = await SendAsync(DomainsEndpoint, new { domains = requestedDomains }, cancellationToken);
        return LinkDetectiveResponseParser.ParseSingleDomain(response, domain, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<LinkDetectiveBatchPricesResponse> GetBatchPricesAsync(
        IReadOnlyCollection<string> domains, CancellationToken cancellationToken)
    {
        var requestedDomains = NormalizeDomains(domains);
        var response = await SendAsync(DomainsEndpoint, new { domains = requestedDomains }, cancellationToken);
        return LinkDetectiveResponseParser.ParseBatch(response, requestedDomains, clock.GetUtcNow().UtcDateTime);
    }

    public async Task<LinkDetectiveQuota> GetQuotaAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(QuotaEndpoint, new { }, cancellationToken);
        return LinkDetectiveQuotaParser.Parse(response.Quota, clock.GetUtcNow().UtcDateTime)
            ?? throw new LinkDetectiveApiException(LinkDetectiveMessages.MissingQuota);
    }

    private async Task<LinkDetectiveApiResponse> SendAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        using var slot = await coordinator.BeginRequestAsync(cancellationToken);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiBaseUrl + endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiToken?.Trim());
            request.Content = JsonContent.Create(body);
            using var response = await clients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            ThrowIfUnsuccessful(response, applyDomainCooldown: endpoint == DomainsEndpoint);
            // Sellers, contacts, SEO metrics and raw responses are never persisted or exposed.
            var payload = await response.Content.ReadFromJsonAsync<LinkDetectiveApiResponse>(cancellationToken)
                ?? throw LinkDetectiveApiException.IncompleteResponse();
            if (endpoint == DomainsEndpoint &&
                LinkDetectiveQuotaParser.GetCooldownUntilUtc(payload.Quota, clock.GetUtcNow().UtcDateTime) is { } retryAtUtc)
            {
                coordinator.BlockUntil(retryAtUtc);
            }
            return payload;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException ||
            exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new LinkDetectiveApiException(LinkDetectiveMessages.RequestFailed);
        }
    }

    private void ThrowIfUnsuccessful(HttpResponseMessage response, bool applyDomainCooldown)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var retryAt = GetRetryAtUtc(response.Headers.RetryAfter);
        // Inventory limits must not block the existing on-demand domain lookup.
        if (applyDomainCooldown)
        {
            coordinator.BlockUntil(retryAt);
        }

        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => LinkDetectiveFailureKind.AccessDenied,
            HttpStatusCode.TooManyRequests => LinkDetectiveFailureKind.RateLimited,
            _ => LinkDetectiveFailureKind.Transient
        };
        var message = kind switch
        {
            LinkDetectiveFailureKind.AccessDenied => LinkDetectiveMessages.AccessDenied,
            LinkDetectiveFailureKind.RateLimited => LinkDetectiveMessages.RateLimited,
            _ => LinkDetectiveMessages.RequestFailed
        };
        throw new LinkDetectiveApiException(message, retryAt, kind);
    }

    private DateTime GetRetryAtUtc(RetryConditionHeaderValue? retryAfter)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var retryAt = retryAfter?.Date?.UtcDateTime ?? now.Add(retryAfter?.Delta ?? DefaultRetryDelay);
        return retryAt > now ? retryAt : now.Add(DefaultRetryDelay);
    }

    private static string[] NormalizeDomains(IReadOnlyCollection<string> domains)
    {
        var normalized = domains
            .Select(DomainNormalizer.Normalize)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            throw new ArgumentException(LinkDetectiveMessages.InvalidDomainBatch, nameof(domains));
        }

        return normalized;
    }
}
