namespace Redhead.SitesCatalog.Domain;

public static class CurrencyCodeValidator
{
    public static bool IsValid(string? code)
        => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');
}
