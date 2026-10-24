using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Moq;
using Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective;
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
        Assert.Equal(new LinkDetectiveDomainPrices(525m, 558.67m, 534m), result);
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
        Assert.Equal(new LinkDetectiveDomainPrices(null, null, null), result);
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
        Assert.Equal(1m, result.MinimumUsd);
        Assert.Equal(1, calls);
        Assert.NotNull(exception.RetryAfterUtc);
    }

    private static LinkDetectiveApiClient CreateClient(HttpClient http, LinkDetectiveRequestCoordinator coordinator)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(value => value.CreateClient(LinkDetectiveApiClient.HttpClientName)).Returns(http);
        return new LinkDetectiveApiClient(factory.Object, Microsoft.Extensions.Options.Options.Create(new LinkDetectiveOptions { ApiToken = "test-token" }), coordinator, TimeProvider.System);
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
