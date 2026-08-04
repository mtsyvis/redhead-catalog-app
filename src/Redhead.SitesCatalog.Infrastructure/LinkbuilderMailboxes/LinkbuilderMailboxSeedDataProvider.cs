using System.Text.Json;

namespace Redhead.SitesCatalog.Infrastructure.LinkbuilderMailboxes;

public static class LinkbuilderMailboxSeedDataProvider
{
    private const string SeedFileName = "linkbuilder-mailbox-aliases.json";

    public static IReadOnlyList<LinkbuilderMailboxSeedRecord> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LinkbuilderMailboxes", "SeedData", SeedFileName);
        if (!File.Exists(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, SeedFileName);
        }

        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var mailboxes = root.GetProperty("currentCanonicalLinkbuilderMailboxes");
        var records = new List<LinkbuilderMailboxSeedRecord>();

        foreach (var mailbox in mailboxes.EnumerateArray())
        {
            var email = Normalize(mailbox.GetProperty("email").GetString());
            var displayName = mailbox.GetProperty("displayName").GetString()?.Trim() ?? email;
            var aliases = new HashSet<string>(StringComparer.Ordinal);

            aliases.Add(email);
            foreach (var alias in mailbox.GetProperty("aliases").EnumerateArray())
            {
                var normalizedAlias = Normalize(alias.GetString());
                if (!string.IsNullOrEmpty(normalizedAlias))
                {
                    aliases.Add(normalizedAlias);
                }
            }

            records.Add(new LinkbuilderMailboxSeedRecord(
                email,
                displayName,
                aliases.OrderBy(alias => alias, StringComparer.Ordinal).ToList()));
        }

        return records;
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
