namespace Redhead.SitesCatalog.Domain.Entities;

public class LinkbuilderMailbox
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<SiteWebmasterOfferLinkbuilderMailbox> OfferLinks { get; set; } = [];
}
