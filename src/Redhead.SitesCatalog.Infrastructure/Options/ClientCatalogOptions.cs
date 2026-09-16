using MimeKit;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class ClientCatalogOptions
{
    public const string SectionName = "ClientCatalog";
    public int RequestsPerMinute { get; set; } = 60;
    public int UniqueSitesPerFiveMinutes { get; set; } = ClientCatalogLimits.DefaultUniqueSitesPerFiveMinutes;
    public int AlertUniqueSitesPerHour { get; set; } = 5000;
    public string AlertEmails { get; set; } = string.Empty;

    public string[] Recipients => AlertEmails.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool IsValid(ClientCatalogOptions options)
        => options.RequestsPerMinute > 0 && options.UniqueSitesPerFiveMinutes > 0 &&
           options.AlertUniqueSitesPerHour > 0 &&
           options.Recipients.All(address => MailboxAddress.TryParse(address, out var mailbox) && mailbox.Address.Contains('@'));
}
