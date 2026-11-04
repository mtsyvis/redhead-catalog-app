using System.Text.Json;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.LinkDetective.Parsing;

internal static class LinkDetectiveJsonReader
{
    public static T? TryRead<T>(JsonElement element) where T : class
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        try
        {
            return element.Deserialize<T>(JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
