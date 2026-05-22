using Redhead.SitesCatalog.Domain.Services;

namespace Redhead.SitesCatalog.Application.Validation;

public static class UserProfileNameValidator
{
    public const int MaxNameLength = 100;

    public static UserProfileNameValidationResult Validate(string? firstName, string? lastName)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedFirstName = Normalize(firstName);
        var normalizedLastName = Normalize(lastName);

        ValidateName("firstName", "First name", normalizedFirstName, errors);
        ValidateName("lastName", "Last name", normalizedLastName, errors);

        return new UserProfileNameValidationResult(
            errors.Count == 0,
            normalizedFirstName,
            normalizedLastName,
            errors);
    }

    public static bool IsProfileComplete(string? firstName, string? lastName)
        => UserProfileNames.IsComplete(firstName, lastName);

    public static string GetDisplayName(string? firstName, string? lastName, string? fallbackEmail)
        => UserProfileNames.GetDisplayName(firstName, lastName, fallbackEmail);

    private static string Normalize(string? value)
        => UserProfileNames.NormalizeName(value);

    private static void ValidateName(
        string fieldName,
        string displayName,
        string value,
        IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[fieldName] = [$"{displayName} is required."];
            return;
        }

        if (value.Length > MaxNameLength)
        {
            errors[fieldName] = [$"{displayName} must be 100 characters or fewer."];
        }
    }
}

public sealed record UserProfileNameValidationResult(
    bool IsValid,
    string FirstName,
    string LastName,
    IReadOnlyDictionary<string, string[]> Errors);
