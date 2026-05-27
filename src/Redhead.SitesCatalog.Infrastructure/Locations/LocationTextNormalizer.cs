using System.Globalization;
using System.Text;

namespace Redhead.SitesCatalog.Infrastructure.Locations;

public static class LocationTextNormalizer
{
    public static string NormalizeLookupValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var withoutDiacritics = RemoveDiacritics(value.Trim());
        var builder = new StringBuilder(withoutDiacritics.Length);
        var previousWasSeparator = false;

        foreach (var character in withoutDiacritics)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
