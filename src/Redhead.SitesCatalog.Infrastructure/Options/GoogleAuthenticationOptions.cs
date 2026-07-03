namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class GoogleAuthenticationOptions
{
    public const string SectionName = "GoogleAuthentication";

    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public static bool IsValid(GoogleAuthenticationOptions options)
        => !options.Enabled ||
           (!string.IsNullOrWhiteSpace(options.ClientId) &&
            !string.IsNullOrWhiteSpace(options.ClientSecret));
}
