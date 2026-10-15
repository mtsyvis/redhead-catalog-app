using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed class WebmasterOffersSummarySearchResult
{
    public string Domain { get; init; } = string.Empty;
    public bool SiteFound { get; init; }
    public IReadOnlyList<WebmasterOfferSummaryDto> Offers { get; init; } = [];
}

// Keep restricted responses independent of the full DTO so new sensitive fields stay private.
public sealed class WebmasterOfferSummaryDto
{
    public Guid Id { get; init; }
    public string? OutreachSenderRawText { get; init; }
    public string? LinkbuilderMailboxRawText { get; init; }
    public string? LinkPolicyText { get; init; }
    public string? DfLinksRawText { get; init; }
    public string? SponsoredTagRawText { get; init; }
    public string? CommentText { get; init; }
    public string? ClientRawText { get; init; }
    public string? TermRawText { get; init; }
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
    public string TermLabel { get; set; } = string.Empty;
    public SiteWebmasterOfferStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public uint Version { get; init; }
    public IReadOnlyList<WebmasterOfferMailboxDto> LinkbuilderMailboxes { get; init; } = [];
}
