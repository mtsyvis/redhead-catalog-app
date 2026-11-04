using System.Text.Json;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Parsing;

// Keep individual rows as JSON so a malformed row cannot discard other batch results.
internal sealed record LinkDetectiveApiResponse(
    JsonElement Results,
    JsonElement NotFound,
    bool Truncated,
    JsonElement Quota);
