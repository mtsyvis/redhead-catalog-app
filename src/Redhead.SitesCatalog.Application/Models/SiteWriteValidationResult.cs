namespace Redhead.SitesCatalog.Application.Models;

public sealed class SiteWriteValidationResult
{
    public bool IsValid => FieldErrors.Count == 0;

    public Dictionary<string, string[]> FieldErrors { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public UpdateSiteRequest? NormalizedRequest { get; init; }
}
