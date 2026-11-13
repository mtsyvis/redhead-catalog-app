using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.Currencies;

public sealed record CurrencyRateTable(DateTime ProviderAsOfUtc, IReadOnlyDictionary<string, decimal> UsdRates,
    DateTime? ProviderNextUpdateUtc = null);

public interface ICurrencyReferenceRateClient
{
    Task<CurrencyRateTable> FetchAsync(CancellationToken cancellationToken);
}

public sealed class ExchangeRateApiClient(IHttpClientFactory clients, IOptions<ExchangeRateApiOptions> options,
    TimeProvider clock) : ICurrencyReferenceRateClient
{
    public const string HttpClientName = "ExchangeRateApi";
    public const string ProviderName = "ExchangeRateAPI";

    public async Task<CurrencyRateTable> FetchAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            throw new InvalidOperationException("ExchangeRate-API is not configured.");
        }

        // The documented Free endpoint requires the key in its path. This named client's HTTP
        // logging and redirects are disabled in DI; never log the URI or a provider response body.
        var key = Uri.EscapeDataString(options.Value.ApiKey);
        using var response = await clients.CreateClient(HttpClientName).GetAsync(
            $"https://v6.exchangerate-api.com/v6/{key}/latest/{CurrencyConversionConstants.DefaultCurrency}", cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return Parse(json, clock.GetUtcNow().UtcDateTime);
    }

    public static CurrencyRateTable Parse(string json, DateTime fetchedAtUtc)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("result").GetString() != "success"
                || root.GetProperty("base_code").GetString() != CurrencyConversionConstants.DefaultCurrency)
            {
                throw new FormatException();
            }

            var timestamp = root.GetProperty("time_last_update_unix").GetInt64();
            var asOf = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
            var next = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("time_next_update_unix").GetInt64()).UtcDateTime;
            if (timestamp <= 0 || asOf > fetchedAtUtc.AddMinutes(5) || next <= asOf)
            {
                throw new FormatException();
            }

            var rates = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var entry in root.GetProperty("conversion_rates").EnumerateObject())
            {
                if (!rates.TryAdd(entry.Name, ParseUsdRate(entry)))
                {
                    throw new FormatException();
                }
            }
            if (!rates.TryGetValue(CurrencyConversionConstants.DefaultCurrency, out var usd)
                || usd != CurrencyConversionConstants.IdentityUsdRate || rates.Count < 2)
            {
                throw new FormatException();
            }

            return new CurrencyRateTable(asOf, rates, next);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException
            or FormatException or OverflowException or ArgumentOutOfRangeException)
        {
            // Do not echo provider errors, request URLs or credentials, even in InnerException.
            throw new FormatException("Invalid ExchangeRate-API reference-rate response.");
        }
    }

    private static decimal ParseUsdRate(JsonProperty currency)
    {
        if (!CurrencyCodeValidator.IsValid(currency.Name) || currency.Value.ValueKind != JsonValueKind.Number
            || !currency.Value.TryGetDecimal(out var units) || units <= 0)
        {
            throw new FormatException();
        }

        // Reject numbers that decimal parsing rounded, and preserve the accepted JSON number format.
        var raw = currency.Value.GetRawText();
        var normalized = raw.Contains('.') ? raw.TrimEnd('0').TrimEnd('.') : raw;
        if (raw.Length > 30 || units.ToString("0.############################", CultureInfo.InvariantCulture) != normalized)
        {
            throw new FormatException();
        }

        var rate = CurrencyConversionConstants.IdentityUsdRate / units;
        if (rate <= 0)
        {
            throw new FormatException();
        }
        return rate;
    }
}
