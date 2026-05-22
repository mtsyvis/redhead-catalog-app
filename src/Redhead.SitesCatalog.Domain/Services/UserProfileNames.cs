namespace Redhead.SitesCatalog.Domain.Services;

public static class UserProfileNames
{
    public static bool IsComplete(string? firstName, string? lastName)
        => !string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName);

    public static string GetDisplayName(string? firstName, string? lastName, string? fallbackEmail)
    {
        if (IsComplete(firstName, lastName))
        {
            return $"{firstName!.Trim()} {lastName!.Trim()}";
        }

        return fallbackEmail ?? string.Empty;
    }

    public static string NormalizeName(string? value)
        => value?.Trim() ?? string.Empty;
}
