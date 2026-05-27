namespace Redhead.SitesCatalog.Domain.Entities;

public class LocationGroupItem
{
    public string GroupKey { get; set; } = string.Empty;
    public LocationGroup? Group { get; set; }

    public string LocationKey { get; set; } = string.Empty;
    public CanonicalLocation? Location { get; set; }
}
