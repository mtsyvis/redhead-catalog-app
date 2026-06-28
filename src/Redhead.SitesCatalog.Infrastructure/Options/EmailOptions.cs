using MimeKit;

namespace Redhead.SitesCatalog.Infrastructure.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public int SendTimeoutSeconds { get; set; } = 10;

    public static bool IsValid(EmailOptions options)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(options.SmtpHost) &&
            options.SmtpPort is > 0 and <= 65535 &&
            !string.IsNullOrWhiteSpace(options.FromName) &&
            IsQualifiedEmailAddress(options.FromAddress) &&
            options.SendTimeoutSeconds is > 0 and <= 60;
    }

    private static bool IsQualifiedEmailAddress(string value)
    {
        if (!MailboxAddress.TryParse(value, out var mailbox))
        {
            return false;
        }

        var separatorIndex = mailbox.Address.LastIndexOf('@');
        return separatorIndex > 0 && separatorIndex < mailbox.Address.Length - 1;
    }
}
