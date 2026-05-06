namespace Redhead.SitesCatalog.Api.Models.Sites;

public sealed class FilterOptionsResponse
{
    public List<FilterOptionResponse> Niches { get; set; } = [];
}

public sealed class FilterOptionResponse
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}
