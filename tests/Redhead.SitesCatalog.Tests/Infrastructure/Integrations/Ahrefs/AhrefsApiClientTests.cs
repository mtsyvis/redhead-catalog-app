using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.Ahrefs;

public sealed class AhrefsApiClientTests
{
    [Fact]
    public void SelectFields_AreExactlyRequiredFieldsInRequiredOrder()
    {
        // Arrange

        // Act
        var fields = AhrefsApiClient.SelectFields;

        // Assert
        Assert.Equal(["index", "org_traffic", "domain_rating"], fields);
    }

    [Fact]
    public async Task RunBatchAnalysisAsync_SendsSelectAndVolumeModeInRequestBody()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"targets":[{"index":0,"org_traffic":123,"domain_rating":45.6}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.Add("x-api-units-cost-total-actual", "50");
            return response;
        });
        var sut = new AhrefsApiClient(
            new StubHttpClientFactory(new HttpClient(handler)),
            Microsoft.Extensions.Options.Options.Create(new AhrefsOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.ahrefs.com/v3"
            }));

        // Act
        var result = await sut.RunBatchAnalysisAsync(
            [new AhrefsBatchTarget("https://example.com", "subdomains", "both")],
            "monthly",
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(
            "https://api.ahrefs.com/v3/batch-analysis/batch-analysis",
            capturedRequest!.RequestUri!.AbsoluteUri);
        using var document = JsonDocument.Parse(capturedBody!);
        var root = document.RootElement;
        Assert.Equal(
            ["index", "org_traffic", "domain_rating"],
            root.GetProperty("select").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("monthly", root.GetProperty("volume_mode").GetString());
        var target = Assert.Single(root.GetProperty("targets").EnumerateArray());
        Assert.Equal("https://example.com", target.GetProperty("url").GetString());
        Assert.Equal("subdomains", target.GetProperty("mode").GetString());
        Assert.Equal("both", target.GetProperty("protocol").GetString());
        Assert.Equal(123, Assert.Single(result.Rows).OrganicTraffic);
        Assert.Equal(50, result.Cost.EffectiveUnits);
    }

    [Fact]
    public async Task RunBatchAnalysisAsync_WhenTargetsArrayIsMissing_Throws()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":[{"index":0,"org_traffic":123,"domain_rating":45.6}]}""",
                    Encoding.UTF8,
                    "application/json")
            };
            response.Headers.Add("x-api-units-cost-total-actual", "61");
            return Task.FromResult(response);
        });
        var sut = new AhrefsApiClient(
            new StubHttpClientFactory(new HttpClient(handler)),
            Microsoft.Extensions.Options.Options.Create(new AhrefsOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.ahrefs.com/v3"
            }));

        // Act
        var exception = await Assert.ThrowsAsync<AhrefsApiException>(
            () => sut.RunBatchAnalysisAsync(
                [new AhrefsBatchTarget("https://example.com", "subdomains", "both")],
                "monthly",
                CancellationToken.None));

        // Assert
        Assert.Contains("required targets array", exception.Message);
        Assert.True(exception.BatchResponseReceived);
        Assert.Equal(61, exception.ActualUnits);
    }

    [Fact]
    public async Task RunBatchAnalysisAsync_WhenRowIndexIsMissing_Throws()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"targets":[{"org_traffic":123,"domain_rating":45.6}]}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var sut = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AhrefsApiException>(
            () => sut.RunBatchAnalysisAsync(
                [new AhrefsBatchTarget("https://example.com", "subdomains", "both")],
                "monthly",
                CancellationToken.None));

        // Assert
        Assert.Contains("valid index", exception.Message);
    }

    [Fact]
    public async Task GetLimitsAndUsageAsync_WhenEnvelopeIsMissing_Throws()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"units_limit_api_key":975000}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var sut = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AhrefsApiException>(
            () => sut.GetLimitsAndUsageAsync(CancellationToken.None));

        // Assert
        Assert.Contains("required limits_and_usage object", exception.Message);
    }

    [Fact]
    public async Task RunBatchAnalysisAsync_WhenMetricHasWrongType_Throws()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"targets":[{"index":0,"org_traffic":"123","domain_rating":45.6}]}""",
                    Encoding.UTF8,
                    "application/json")
            }));
        var sut = CreateClient(handler);

        // Act
        var exception = await Assert.ThrowsAsync<AhrefsApiException>(
            () => sut.RunBatchAnalysisAsync(
                [new AhrefsBatchTarget("https://example.com", "subdomains", "both")],
                "monthly",
                CancellationToken.None));

        // Assert
        Assert.Contains("org_traffic was not a valid integer", exception.Message);
    }

    private static AhrefsApiClient CreateClient(HttpMessageHandler handler)
        => new(
            new StubHttpClientFactory(new HttpClient(handler)),
            Microsoft.Extensions.Options.Options.Create(new AhrefsOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://api.ahrefs.com/v3"
            }));

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _handler(request);
    }
}
