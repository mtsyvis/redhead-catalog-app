using System.Net;
using System.Text;
using Moq;
using Redhead.SitesCatalog.Infrastructure.Integrations.Currencies;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Tests.Infrastructure.Integrations.Currencies;

public sealed class ExchangeRateApiClientTests
{
    private readonly TestClock _clock = new();
    private readonly ExchangeRateApiOptions _options = new();

    [Theory]
    [InlineData("1.25", "0.8")]
    [InlineData("1.2500", "0.8")]
    [InlineData("1000", "0.001")]
    [InlineData("0.5", "2")]
    public void ExchangeRateParser_InvertsUsdBaseAndKeepsProviderTime(string providerRate, string expectedUsdRate)
    {
        // Arrange
        var json = ExchangeRateJson(providerRate);
        var expected = decimal.Parse(expectedUsdRate, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var table = ExchangeRateApiClient.Parse(json, _clock.GetUtcNow().UtcDateTime);

        // Assert
        Assert.Equal(expected, table.UsdRates["CAD"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794448800).UtcDateTime, table.ProviderAsOfUtc);
    }

    [Fact]
    public void ExchangeRateParser_RejectsProviderErrorsWithoutEchoingThem()
    {
        // Arrange
        const string error = "invalid-key";
        var json = "{\"result\":\"error\",\"error-type\":\"" + error + "\"}";

        // Act
        var exception = Record.Exception(() => ExchangeRateApiClient.Parse(json, _clock.GetUtcNow().UtcDateTime));

        // Assert
        Assert.IsType<FormatException>(exception);
        Assert.DoesNotContain(error, exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"NaN\"")]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("9.9999999999999999999999999999")]
    [InlineData("1e999")]
    [InlineData("1e0")]
    [InlineData("\"1.25\"")]
    [InlineData("true")]
    [InlineData("null")]
    public void ExchangeRateParser_RejectsInvalidOrOverpreciseRates(string rate)
    {
        // Arrange
        var json = ExchangeRateJson(rate);

        // Act
        var exception = Record.Exception(() => ExchangeRateApiClient.Parse(json, _clock.GetUtcNow().UtcDateTime));

        // Assert
        Assert.IsType<FormatException>(exception);
        Assert.DoesNotContain(rate, exception.Message);
    }

    [Theory]
    [InlineData("\"base_code\":\"USD\"", "\"base_code\":\"EUR\"")]
    [InlineData("1794448800", "0")]
    [InlineData("1794535200", "1")]
    [InlineData("\"USD\":1", "\"USD\":2")]
    [InlineData("\"CAD\":1.25,", "")]
    [InlineData("\"CAD\":1.25", "\"CAD\":1.25,\"CAD\":1.25")]
    public void ExchangeRateParser_RejectsInvalidBaseTimestampAndMissingCurrency(string from, string to)
    {
        // Arrange
        var json = ExchangeRateJson("1.25").Replace(from, to, StringComparison.Ordinal);

        // Act
        var exception = Record.Exception(() => ExchangeRateApiClient.Parse(json, _clock.GetUtcNow().UtcDateTime));

        // Assert
        Assert.IsType<FormatException>(exception);
    }

    [Fact]
    public async Task ExchangeRateClient_UsesDocumentedFreeEndpoint()
    {
        // Arrange
        Uri? uri = null;
        var handler = new ResponseHandler(request =>
        {
            uri = request.RequestUri;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(ExchangeRateJson("1.25"), Encoding.UTF8, "application/json") });
        });
        using var http = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(ExchangeRateApiClient.HttpClientName)).Returns(http);
        _options.ApiKey = "synthetic-test-key";
        var client = new ExchangeRateApiClient(factory.Object, Microsoft.Extensions.Options.Options.Create(_options), _clock);

        // Act
        var result = await client.FetchAsync(CancellationToken.None);

        // Assert
        Assert.Equal("https://v6.exchangerate-api.com/v6/synthetic-test-key/latest/USD", uri!.AbsoluteUri);
        Assert.Equal(0.8m, result.UsdRates["CAD"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794535200).UtcDateTime, result.ProviderNextUpdateUtc);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task ExchangeRateClient_HttpFailuresDoNotExposeTheKeyOrBody(HttpStatusCode status)
    {
        // Arrange
        var handler = new ResponseHandler(_ => Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent("synthetic-private-provider-body") }));
        using var http = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(ExchangeRateApiClient.HttpClientName)).Returns(http);
        _options.ApiKey = "synthetic-test-key";
        var client = new ExchangeRateApiClient(factory.Object, Microsoft.Extensions.Options.Options.Create(_options), _clock);

        // Act
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync(CancellationToken.None));

        // Assert
        Assert.DoesNotContain(_options.ApiKey, exception.ToString());
        Assert.DoesNotContain("synthetic-private-provider-body", exception.ToString());
    }

    private static string ExchangeRateJson(string cadRate) => "{\"result\":\"success\",\"base_code\":\"USD\",\"time_last_update_unix\":1794448800,\"time_next_update_unix\":1794535200,\"conversion_rates\":{\"CAD\":" + cadRate + ",\"USD\":1}}";

    private sealed class TestClock : TimeProvider
    {
        private readonly DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ResponseHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
