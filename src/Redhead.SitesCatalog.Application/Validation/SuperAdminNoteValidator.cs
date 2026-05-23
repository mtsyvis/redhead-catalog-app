namespace Redhead.SitesCatalog.Application.Validation;

public static class SuperAdminNoteValidator
{
    public const int MaxLength = 1000;

    public static SuperAdminNoteValidationResult Validate(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

        if (normalized is { Length: > MaxLength })
        {
            return new SuperAdminNoteValidationResult(
                false,
                normalized,
                "Super Admin note must be 1000 characters or fewer.");
        }

        return new SuperAdminNoteValidationResult(true, normalized, null);
    }
}

public sealed record SuperAdminNoteValidationResult(
    bool IsValid,
    string? Value,
    string? Error);
