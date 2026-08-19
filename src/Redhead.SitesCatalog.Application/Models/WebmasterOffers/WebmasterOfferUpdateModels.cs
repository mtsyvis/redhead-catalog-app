using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed class WebmasterOfferEditDto
{
    public WebmasterOfferDto Offer { get; init; } = new();
    public IReadOnlyList<WebmasterOfferMailboxOptionDto> AvailableMailboxes { get; init; } = [];
}

public sealed class WebmasterOfferMailboxOptionDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class UpdateWebmasterOfferRequest
{
    public DateTime ExpectedUpdatedAtUtc { get; set; }
    public SiteWebmasterOfferStatus Status { get; set; }
    public string? OutreachSenderRawText { get; set; }
    public string? LinkPolicyText { get; set; }
    public string? DfLinksRawText { get; set; }
    public string? SponsoredTagRawText { get; set; }
    public string? CommentText { get; set; }
    public string? ClientRawText { get; set; }
    public TermType? TermType { get; set; }
    public int? TermValue { get; set; }
    public TermUnit? TermUnit { get; set; }
    public List<Guid> LinkbuilderMailboxIds { get; set; } = [];
    public List<UpdateWebmasterOfferPriceRequest> Prices { get; set; } = [];
}

public sealed class UpdateWebmasterOfferPriceRequest
{
    public WebmasterOfferPriceType PriceType { get; set; }
    public ServiceAvailabilityStatus AvailabilityStatus { get; set; }
    public decimal? WebmasterPriceUsd { get; set; }
    public string? WebmasterPriceDetails { get; set; }
}

public enum WebmasterOfferUpdateStatus
{
    Success,
    NotFound,
    Conflict
}

public sealed class WebmasterOfferUpdateResult
{
    public WebmasterOfferUpdateStatus Status { get; init; }
    public WebmasterOfferDto? Offer { get; init; }
}
