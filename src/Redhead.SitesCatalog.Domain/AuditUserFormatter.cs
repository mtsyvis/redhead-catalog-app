namespace Redhead.SitesCatalog.Domain;

public static class AuditUserFormatter
{
    public static string Format(string? value)
        => string.IsNullOrWhiteSpace(value) ? "system" : value.Trim();
}
