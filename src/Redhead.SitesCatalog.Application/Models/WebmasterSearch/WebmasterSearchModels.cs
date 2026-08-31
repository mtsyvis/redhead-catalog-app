using Redhead.SitesCatalog.Application.Models.WebmasterOffers;

namespace Redhead.SitesCatalog.Application.Models.WebmasterSearch;

public sealed class WebmasterSearchResult
{
    public IReadOnlyList<WebmasterSearchItemDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int Total { get; init; }
}

public sealed class WebmasterSearchItemDto
{
    public Guid WebmasterId { get; init; }
    public string? PrimaryEmail { get; init; }
    public string RepresentativeContactRawText { get; init; } = string.Empty;
    public string MatchingContactSnippet { get; init; } = string.Empty;
    public int OfferCount { get; init; }
    public int ActiveOfferCount { get; init; }
    public int DomainCount { get; init; }
    public DateTime LatestOfferUpdatedAtUtc { get; init; }
}

public sealed class WebmasterWorkspaceDto
{
    public WebmasterWorkspaceSummaryDto Webmaster { get; init; } = new();
    public IReadOnlyList<WebmasterWorkspaceDomainDto> Domains { get; init; } = [];
}

public sealed class WebmasterWorkspaceSummaryDto
{
    public Guid WebmasterId { get; init; }
    public string? PrimaryEmail { get; init; }
    public string RepresentativeContactRawText { get; init; } = string.Empty;
    public int OfferCount { get; init; }
    public int ActiveOfferCount { get; init; }
    public int DomainCount { get; init; }
}

public sealed class WebmasterWorkspaceDomainDto
{
    public string Domain { get; init; } = string.Empty;
    public bool SiteFound { get; init; }
    public bool IsQuarantined { get; init; }
    public string? QuarantineReason { get; init; }
    public int OtherWebmasterOfferCount { get; init; }
    public IReadOnlyList<WebmasterOfferDto> Offers { get; init; } = [];
}
