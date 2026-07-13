namespace Redhead.SitesCatalog.Infrastructure.LinkbuilderMailboxes;

public sealed record LinkbuilderMailboxSeedRecord(
    string Email,
    string DisplayName,
    IReadOnlyList<string> Aliases);
