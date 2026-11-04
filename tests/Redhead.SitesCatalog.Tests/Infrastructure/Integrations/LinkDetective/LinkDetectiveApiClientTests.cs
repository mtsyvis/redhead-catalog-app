using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Coordination;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Errors;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Models;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.LinkDetective;

public sealed class LinkDetectiveApiClientTests
{
    [Fact]
    public async Task GetPricesAsync_ReturnsProviderAggregatesInsteadOfCalculatingFromSellers()
    {
        // Arrange
        string? body = null;
        string? authorization = null;
        Uri? uri = null;
        using var handler = new StubHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            authorization = request.Headers.Authorization?.ToString();
            uri = request.RequestUri;
            return JsonResponse("""{"results":[{"domain":"example.com","price":525,"avg":558.67,"median":534,"sellers":[{"price":900,"email":"private@example.com"}]}],"notFound":[],"truncated":false}""");
        });
        using var http = new HttpClient(handler);
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var service = CreateClient(http, coordinator);

        // Act
        var result = await service.GetPricesAsync("example.com", CancellationToken.None);

        // Assert
        Assert.Equal(new LinkDetectivePrices(525m, 558.67m, 534m), result.Prices);
        Assert.Null(result.Quota);
        Assert.Equal("https://linkdetective.pro/api/v2/domains", uri!.AbsoluteUri);
        Assert.Equal("Bearer test-token", authorization);
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("example.com", Assert.Single(json.RootElement.GetProperty("domains").EnumerateArray()).GetString());
        Assert.DoesNotContain("private@example.com", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("""{"results":[],"notFound":["example.com"]}""")]
    [InlineData("""{"results":[],"notFound":["https://www.example.com/path"]}""")]
    public async Task GetPricesAsync_ExplicitNotFoundReturnsEmptyPrices(string payload)
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(payload))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetPricesAsync("example.com", CancellationToken.None);

        // Assert
        Assert.Equal(new LinkDetectivePrices(null, null, null), result.Prices);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("""{"results":null,"notFound":["example.com"]}""")]
    [InlineData("""{"results":[null],"notFound":[]}""")]
    [InlineData("""{"results":[null,{"domain":"example.com","price":1,"avg":2,"median":2}],"notFound":[]}""")]
    [InlineData("""{"results":[],"notFound":[]}""")]
    [InlineData("""{"results":[{"domain":"other.example","price":1,"avg":2,"median":2}]}""")]
    [InlineData("""{"results":[{"domain":"example.com","price":1,"median":2}]}""")]
    [InlineData("""{"results":[{"domain":"example.com","price":-1,"avg":2,"median":2}]}""")]
    [InlineData("""{"results":[],"notFound":["example.com"],"truncated":true}""")]
    [InlineData("""{"results":[{"domain":"example.com","price":1,"avg":2,"median":2}],"quota":{"remaining":"invalid"}}""")]
    [InlineData("not-json-private-response")]
    public async Task GetPricesAsync_IncompleteResponseIsNotTreatedAsNotFound(string payload)
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(payload))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var exception = await Assert.ThrowsAsync<LinkDetectiveApiException>(
            () => CreateClient(http, coordinator).GetPricesAsync("example.com", CancellationToken.None));

        // Assert
        Assert.DoesNotContain("private-response", exception.ToString());
        Assert.DoesNotContain("test-token", exception.ToString());
    }

    [Fact]
    public async Task GetPricesAsync_RateLimitBlocksOtherDomainsWithoutRetrying()
    {
        // Arrange
        var calls = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("private upstream details")
            };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
            return Task.FromResult(response);
        }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var client = CreateClient(http, coordinator);

        // Act
        var first = await Assert.ThrowsAsync<LinkDetectiveApiException>(() => client.GetPricesAsync("example.com", CancellationToken.None));
        var second = await Assert.ThrowsAsync<LinkDetectiveApiException>(() => client.GetPricesAsync("other.example", CancellationToken.None));

        // Assert
        Assert.Equal(1, calls);
        Assert.NotNull(first.RetryAfterUtc);
        Assert.Equal(first.RetryAfterUtc, second.RetryAfterUtc);
        Assert.DoesNotContain("private upstream", first.ToString());
    }

    [Fact]
    public async Task GetPricesAsync_ExhaustedQuotaKeepsLastResultButBlocksAnotherCheck()
    {
        // Arrange
        var calls = 0;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            calls++;
            return Task.FromResult(JsonResponse("""{"results":[{"domain":"example.com","price":1,"avg":2,"median":2}],"quota":{"remaining":0,"period":"day"}}"""));
        }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var client = CreateClient(http, coordinator);

        // Act
        var result = await client.GetPricesAsync("example.com", CancellationToken.None);
        var exception = await Assert.ThrowsAsync<LinkDetectiveApiException>(() => client.GetPricesAsync("other.example", CancellationToken.None));

        // Assert
        Assert.Equal(1m, result.Prices.MinimumUsd);
        Assert.Equal(1, calls);
        Assert.NotNull(exception.RetryAfterUtc);
    }

    [Fact]
    public async Task GetBatchPricesAsync_PreservesConfirmedPartialResultsAndSeparatesUnresolvedDomains()
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse("""
            {"results":[{"domain":"a.example","price":10,"avg":20,"median":15},
            {"domain":"duplicate.example","price":1,"avg":2,"median":2},
            {"domain":"duplicate.example","price":2,"avg":3,"median":3}],
            "notFound":["b.example"],"truncated":true,
            "quota":{"limit":50000,"used":4,"remaining":49996,"period":"day"}}
            """))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetBatchPricesAsync(
            ["a.example", "b.example", "omitted.example", "duplicate.example"], CancellationToken.None);

        // Assert
        Assert.Equal(2, result.PricesByDomain.Count);
        Assert.Equal(10, result.PricesByDomain["a.example"].MinimumUsd);
        Assert.Null(result.PricesByDomain["b.example"].MinimumUsd);
        Assert.DoesNotContain("omitted.example", result.PricesByDomain.Keys);
        Assert.DoesNotContain("duplicate.example", result.PricesByDomain.Keys);
        Assert.True(result.IsTruncated);
        Assert.Equal(49996, result.Quota!.Remaining);
    }

    [Fact]
    public async Task GetBatchPricesAsync_DeduplicatesNormalizedDomainsBeforeSending()
    {
        // Arrange
        string? body = null;
        using var http = new HttpClient(new StubHandler(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse("""{"results":[],"notFound":["example.com"]}""");
        }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetBatchPricesAsync(
            ["example.com", "https://www.EXAMPLE.com/path"], CancellationToken.None);

        // Assert
        using var json = JsonDocument.Parse(body!);
        Assert.Single(json.RootElement.GetProperty("domains").EnumerateArray());
        Assert.Single(result.PricesByDomain);
    }

    [Fact]
    public async Task GetQuotaAsync_UsesFreePreviewAndDoesNotRequestDomains()
    {
        // Arrange
        string? endpoint = null;
        using var http = new HttpClient(new StubHandler(request =>
        {
            endpoint = request.RequestUri!.AbsolutePath;
            return Task.FromResult(JsonResponse("""{"quota":{"limit":50000,"used":100,"remaining":49900,"period":"day","resetAt":"2026-11-04T00:00:00Z"}}"""));
        }));
        var clock = new FixedClock(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));
        using var coordinator = new LinkDetectiveRequestCoordinator(clock);

        // Act
        var quota = await CreateClient(http, coordinator, clock).GetQuotaAsync(CancellationToken.None);

        // Assert
        Assert.Equal("/api/v2/inventory/preview", endpoint);
        Assert.Equal(49900, quota.Remaining);
        Assert.Equal(new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc), quota.ResetAtUtc);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"domain\":\"broken.example\",\"price\":\"invalid\",\"avg\":20,\"median\":15}")]
    public async Task GetBatchPricesAsync_MalformedRowDoesNotDiscardOtherConfirmedResults(string malformedRow)
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(
            "{\"results\":[{\"domain\":\"valid.example\",\"price\":10,\"avg\":20,\"median\":15}," + malformedRow +
            "],\"notFound\":[\"absent.example\"]}"))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetBatchPricesAsync(
            ["valid.example", "broken.example", "absent.example"], CancellationToken.None);

        // Assert
        Assert.Equal(2, result.PricesByDomain.Count);
        Assert.Equal(10m, result.PricesByDomain["valid.example"].MinimumUsd);
        Assert.Null(result.PricesByDomain["absent.example"].MinimumUsd);
        Assert.DoesNotContain("broken.example", result.PricesByDomain.Keys);
    }

    [Fact]
    public async Task GetBatchPricesAsync_MalformedDuplicateCannotEstablishWinningPrice()
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse("""
            {"results":[{"domain":"example.com","price":10,"avg":20,"median":15},
            {"domain":"example.com","price":"invalid","avg":20,"median":15}]}
            """))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetBatchPricesAsync(["example.com"], CancellationToken.None);

        // Assert
        Assert.Empty(result.PricesByDomain);
    }

    [Fact]
    public async Task GetBatchPricesAsync_MalformedQuotaDoesNotEraseConfirmedPrices()
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse("""
            {"results":[{"domain":"example.com","price":10,"avg":20,"median":15}],"quota":{"remaining":"invalid"}}
            """))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var result = await CreateClient(http, coordinator).GetBatchPricesAsync(["example.com"], CancellationToken.None);

        // Assert
        Assert.Equal(10m, result.PricesByDomain["example.com"].MinimumUsd);
        Assert.Null(result.Quota);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Preview_InventoryQuotaOrAccessFailureDoesNotBlockOnDemandDomainLookup(int previewStatus)
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(request => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith("/preview", StringComparison.Ordinal)
                ? previewStatus != 200 ? new HttpResponseMessage((HttpStatusCode)previewStatus) : JsonResponse("""
                    {"quota":{"limit":500000,"used":500000,"remaining":0,"period":"billing-period"}}
                    """)
                : JsonResponse("""{"results":[{"domain":"example.com","price":10,"avg":20,"median":15}]}"""))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var client = CreateClient(http, coordinator);
        if (previewStatus != 200)
        {
            await Assert.ThrowsAsync<LinkDetectiveApiException>(() => client.GetQuotaAsync(CancellationToken.None));
        }
        else
        {
            await client.GetQuotaAsync(CancellationToken.None);
        }

        // Act
        var result = await client.GetPricesAsync("example.com", CancellationToken.None);

        // Assert
        Assert.Equal(10m, result.Prices.MinimumUsd);
    }

    [Fact]
    public async Task GetPricesAsync_ReturnsQuotaAlongsidePrices()
    {
        // Arrange
        var now = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FixedClock(now);
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse("""
            {"results":[{"domain":"example.com","price":10,"avg":20,"median":15}],
             "quota":{"limit":50000,"used":1,"remaining":49999,"period":"day"}}
            """))));
        using var coordinator = new LinkDetectiveRequestCoordinator(clock);

        // Act
        var response = await CreateClient(http, coordinator, clock).GetPricesAsync("example.com", CancellationToken.None);

        // Assert
        Assert.Equal(new LinkDetectivePrices(10, 20, 15), response.Prices);
        Assert.Equal(new LinkDetectiveQuota(50000, 1, 49999, "day", now.Date.AddDays(1)), response.Quota);
    }

    [Theory]
    [InlineData("day", null, "2026-11-04T00:00:00Z")]
    [InlineData("billing-period", null, null)]
    [InlineData("day", "2026-11-04T08:00:00+08:00", "2026-11-04T00:00:00Z")]
    [InlineData("day", "2026-11-03T12:00:00Z", null)]
    [InlineData("day", "2026-11-03T11:59:59Z", null)]
    [InlineData("billing-period", "2026-12-01T00:00:00Z", "2026-12-01T00:00:00Z")]
    public async Task GetQuotaAsync_NormalizesResetBeforeReturningQuota(string period, string? resetAt, string? expectedResetAt)
    {
        // Arrange
        var clock = new FixedClock(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc));
        var payload = JsonSerializer.Serialize(new
        {
            quota = new { limit = 50000, used = 100, remaining = 49900, period, resetAt }
        });
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(payload))));
        using var coordinator = new LinkDetectiveRequestCoordinator(clock);
        var expected = expectedResetAt is null ? (DateTime?)null : DateTimeOffset.Parse(expectedResetAt).UtcDateTime;

        // Act
        var quota = await CreateClient(http, coordinator, clock).GetQuotaAsync(CancellationToken.None);

        // Assert
        Assert.Equal(expected, quota.ResetAtUtc);
    }

    [Theory]
    [InlineData("""{"results":[],"notFound":["example.com","example.com"]}""")]
    [InlineData("""{"results":[{"domain":"example.com","price":10,"avg":20,"median":15}],"notFound":["example.com"]}""")]
    public async Task GetBatchPricesAsync_ContradictoryResultsRemainUnresolved(string payload)
    {
        // Arrange
        using var http = new HttpClient(new StubHandler(_ => Task.FromResult(JsonResponse(payload))));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var response = await CreateClient(http, coordinator).GetBatchPricesAsync(["example.com"], CancellationToken.None);

        // Assert
        Assert.Empty(response.PricesByDomain);
    }

    [Fact]
    public async Task GetBatchPricesAsync_DoesNotAssumeFiveHundredIsTheProviderMaximum()
    {
        // Arrange
        var domains = Enumerable.Range(1, 750).Select(index => $"site{index}.example").ToArray();
        string? requestBody = null;
        var payload = JsonSerializer.Serialize(new { results = Array.Empty<object>(), notFound = domains });
        using var http = new HttpClient(new StubHandler(async request =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(payload);
        }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);

        // Act
        var response = await CreateClient(http, coordinator).GetBatchPricesAsync(domains, CancellationToken.None);

        // Assert
        using var requestJson = JsonDocument.Parse(requestBody!);
        var sentDomains = requestJson.RootElement.GetProperty("domains").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Equal(domains, sentDomains);
        Assert.Equal(domains.Length, response.PricesByDomain.Count);
    }

    [Fact]
    public async Task GetBatchPricesAsync_RejectsEmptyBatchBeforeSendingRequest()
    {
        // Arrange
        var requestSent = false;
        using var http = new HttpClient(new StubHandler(_ =>
        {
            requestSent = true;
            return Task.FromResult(JsonResponse("{}"));
        }));
        using var coordinator = new LinkDetectiveRequestCoordinator(TimeProvider.System);
        var client = CreateClient(http, coordinator);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.GetBatchPricesAsync([], CancellationToken.None));

        // Assert
        Assert.Equal("domains", exception.ParamName);
        Assert.False(requestSent);
    }

    private static LinkDetectiveApiClient CreateClient(
        HttpClient http, LinkDetectiveRequestCoordinator coordinator, TimeProvider? clock = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(LinkDetectiveApiClient.HttpClientName)).Returns(http);
        return new LinkDetectiveApiClient(factory.Object, Microsoft.Extensions.Options.Options.Create(new LinkDetectiveOptions { ApiToken = "test-token" }), coordinator, clock ?? TimeProvider.System);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
