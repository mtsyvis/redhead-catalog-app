namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed class WebmasterOffersMultiSearchRequest
{
    public string QueryText { get; set; } = string.Empty;
}

public sealed class WebmasterOffersMultiSearchResult<TOffer>
{
    public int InputCount { get; init; }
    public int UniqueDomainCount { get; init; }
    public IReadOnlyList<string> Duplicates { get; init; } = [];
    public IReadOnlyList<string> DomainsWithoutOffers { get; init; } = [];
    public IReadOnlyList<string> NotFoundDomains { get; init; } = [];
    public IReadOnlyList<WebmasterOffersDomainResult<TOffer>> Results { get; init; } = [];
}

public sealed class WebmasterOffersDomainResult<TOffer>
{
    public string Domain { get; init; } = string.Empty;
    public IReadOnlyList<TOffer> Offers { get; init; } = [];
}
