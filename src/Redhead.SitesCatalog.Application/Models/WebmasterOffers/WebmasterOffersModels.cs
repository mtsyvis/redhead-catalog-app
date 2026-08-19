using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed class WebmasterOffersSearchResult
{
    public string Domain { get; init; } = string.Empty;
    public bool SiteFound { get; init; }
    public IReadOnlyList<WebmasterOfferDto> Offers { get; init; } = [];
}

public sealed class WebmasterOfferDto
{
    public Guid Id { get; init; }
    public string? PrimaryEmail { get; init; }
    public string ContactRawText { get; init; } = string.Empty;
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
    public string TermLabel { get; init; } = string.Empty;
    public SiteWebmasterOfferStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string? UpdatedBy { get; init; }
    public IReadOnlyList<WebmasterOfferMailboxDto> LinkbuilderMailboxes { get; init; } = [];
    public IReadOnlyList<WebmasterOfferPriceDto> Prices { get; init; } = [];
}

public sealed class WebmasterOfferMailboxDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public LinkbuilderMailboxOfferSource Source { get; init; }
}

public sealed class WebmasterOfferPriceDto
{
    public Guid Id { get; init; }
    public WebmasterOfferPriceType PriceType { get; init; }
    public ServiceAvailabilityStatus AvailabilityStatus { get; init; }
    public decimal? WebmasterPriceUsd { get; init; }
    public string? WebmasterPriceDetails { get; init; }
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
    public string TermLabel { get; init; } = string.Empty;
}
