namespace Redhead.SitesCatalog.Domain.Entities;

public sealed class ClientCatalogRequest
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string[] Domains { get; set; } = [];
}
