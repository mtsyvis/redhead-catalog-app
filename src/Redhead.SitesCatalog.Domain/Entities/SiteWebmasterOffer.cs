using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Domain.Entities;

public class SiteWebmasterOffer
{
    public Guid Id { get; set; }

    public string SiteDomain { get; set; } = string.Empty;
    public Site Site { get; set; } = null!;

    public Guid WebmasterId { get; set; }
    public Webmaster Webmaster { get; set; } = null!;

    public string ImportFingerprint { get; set; } = string.Empty;
    public string ContactRawText { get; set; } = string.Empty;
    public string? OutreachSenderRawText { get; set; }
    public string? LinkbuilderMailboxRawText { get; set; }
    public string? LinkPolicyText { get; set; }
    public string? CommentText { get; set; }
    public string? ClientRawText { get; set; }
    public string? TermRawText { get; set; }
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public SiteWebmasterOfferStatus Status { get; set; } = SiteWebmasterOfferStatus.Active;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<SiteWebmasterOfferLinkbuilderMailbox> LinkbuilderMailboxes { get; set; } = [];
    public ICollection<WebmasterOfferPrice> Prices { get; set; } = [];
}
