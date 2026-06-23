namespace Redhead.SitesCatalog.Application.Validation;

public static class UserDisplayNameValidator
{
    public const int MaxLength = 100;

    public static UserDisplayNameValidationResult Validate(string? displayName)
    {
        var normalized = displayName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return new UserDisplayNameValidationResult(
                false,
                normalized,
                new Dictionary<string, string[]>
                {
                    ["displayName"] = ["Display name is required."]
                });
        }

        if (normalized.Length > MaxLength)
        {
            return new UserDisplayNameValidationResult(
                false,
                normalized,
                new Dictionary<string, string[]>
                {
                    ["displayName"] = [$"Display name must be {MaxLength} characters or fewer."]
                });
        }

        return new UserDisplayNameValidationResult(
            true,
            normalized,
            new Dictionary<string, string[]>());
    }
}

public sealed record UserDisplayNameValidationResult(
    bool IsValid,
    string DisplayName,
    IReadOnlyDictionary<string, string[]> Errors);
