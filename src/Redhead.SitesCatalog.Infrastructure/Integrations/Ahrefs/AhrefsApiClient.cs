using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.Ahrefs;

public sealed class AhrefsApiClient : IAhrefsApiClient
{
    public static readonly string[] SelectFields = ["index", "org_traffic", "domain_rating"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(3)
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AhrefsOptions _options;

    public AhrefsApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AhrefsOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<AhrefsLimitsAndUsage> GetLimitsAndUsageAsync(
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () => CreateRequest(HttpMethod.Get, "subscription-info/limits-and-usage"),
            cancellationToken);
        await EnsureSuccessAsync(response, "Ahrefs limits and usage request failed.", cancellationToken);

        using var document = await ReadJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("limits_and_usage", out var limits) ||
            limits.ValueKind != JsonValueKind.Object)
        {
            throw new AhrefsApiException(
                "Ahrefs limits response did not contain the required limits_and_usage object.");
        }

        return new AhrefsLimitsAndUsage(
            GetRequiredInt64(limits, "units_limit_workspace"),
            GetRequiredInt64(limits, "units_usage_workspace"),
            GetRequiredInt64(limits, "units_limit_api_key"),
            GetRequiredInt64(limits, "units_usage_api_key"),
            GetNullableDateTime(limits, "usage_reset_date"));
    }

    public async Task<AhrefsBatchResult> RunBatchAnalysisAsync(
        IReadOnlyList<AhrefsBatchTarget> targets,
        string volumeMode,
        CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(
            () =>
            {
                var request = CreateRequest(HttpMethod.Post, "batch-analysis/batch-analysis");
                request.Content = JsonContent.Create(
                    new
                    {
                        select = SelectFields,
                        volume_mode = volumeMode,
                        targets = targets.Select(target => new
                        {
                            url = target.Url,
                            mode = target.Mode,
                            protocol = target.Protocol
                        })
                    },
                    options: JsonOptions);
                return request;
            },
            cancellationToken);
        await EnsureSuccessAsync(response, "Ahrefs Batch Analysis request failed.", cancellationToken);

        var cost = ReadCostHeaders(response);
        try
        {
            using var document = await ReadJsonAsync(response, cancellationToken);
            var rowsElement = ResolveRows(document.RootElement);
            var rows = new List<AhrefsBatchRow>();
            foreach (var row in rowsElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    !row.TryGetProperty("index", out var indexElement) ||
                    indexElement.ValueKind != JsonValueKind.Number ||
                    !indexElement.TryGetInt32(out var index))
                {
                    throw new AhrefsApiException(
                        "Ahrefs Batch Analysis returned a row without a valid index.");
                }

                rows.Add(new AhrefsBatchRow(
                    index,
                    GetNullableInt64(row, "org_traffic"),
                    GetNullableDouble(row, "domain_rating"),
                    GetNullableString(row, "error")));
            }

            return new AhrefsBatchResult(rows, cost);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or AhrefsApiException)
        {
            throw new AhrefsApiException(
                ex.Message,
                actualUnits: cost.EffectiveUnits > 0 ? cost.EffectiveUnits : null,
                batchResponseReceived: true,
                innerException: ex);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            using var request = requestFactory();
            var response = await _httpClientFactory
                .CreateClient(nameof(AhrefsApiClient))
                .SendAsync(request, cancellationToken);
            if (!IsTransient(response.StatusCode) || attempt >= RetryDelays.Length)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new AhrefsApiException("Ahrefs API key is not configured.");
        }

        var baseUrl = _options.BaseUrl.TrimEnd('/') + "/";
        var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return request;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (detail.Length > 500)
        {
            detail = detail[..500];
        }

        throw new AhrefsApiException(
            string.IsNullOrWhiteSpace(detail) ? message : $"{message} {detail}",
            (int)response.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static JsonElement ResolveRows(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("targets", out var targets) &&
            targets.ValueKind == JsonValueKind.Array)
        {
            return targets;
        }

        throw new AhrefsApiException(
            "Ahrefs Batch Analysis response did not contain the required targets array.");
    }

    private static AhrefsBatchCost ReadCostHeaders(HttpResponseMessage response)
        => new(
            ReadInt64Header(response, "x-api-rows"),
            ReadInt64Header(response, "x-api-units-cost-row"),
            ReadInt64Header(response, "x-api-units-cost-total"),
            ReadInt64Header(response, "x-api-units-cost-total-actual"),
            ReadHeader(response, "x-api-cache"));

    private static long? ReadInt64Header(HttpResponseMessage response, string name)
        => long.TryParse(ReadHeader(response, name), out var value) ? value : null;

    private static string? ReadHeader(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static long GetRequiredInt64(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.Number &&
           property.TryGetInt64(out var value)
            ? value
            : throw new AhrefsApiException($"Ahrefs response did not contain {propertyName}.");

    private static long? GetNullableInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt64(out var value)
            ? value
            : throw new AhrefsApiException(
                $"Ahrefs response property {propertyName} was not a valid integer.");
    }

    private static double? GetNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number &&
               property.TryGetDouble(out var value)
            ? value
            : throw new AhrefsApiException(
                $"Ahrefs response property {propertyName} was not a valid number.");
    }

    private static DateTime? GetNullableDateTime(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String &&
               property.TryGetDateTime(out var value)
            ? value
            : throw new AhrefsApiException(
                $"Ahrefs response property {propertyName} was not a valid date/time.");
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : throw new AhrefsApiException(
                $"Ahrefs response property {propertyName} was not a valid string.");
    }
}
