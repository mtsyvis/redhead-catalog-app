using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SiteWebmasterOfferLinkbuilderMailbox
{
    public Guid SiteWebmasterOfferId { get; set; }
    public SiteWebmasterOffer SiteWebmasterOffer { get; set; } = null!;

    public Guid LinkbuilderMailboxId { get; set; }
    public LinkbuilderMailbox LinkbuilderMailbox { get; set; } = null!;

    public LinkbuilderMailboxOfferSource Source { get; set; } = LinkbuilderMailboxOfferSource.Import;
    public DateTime CreatedAtUtc { get; set; }
}
