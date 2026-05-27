namespace Redhead.SitesCatalog.Domain.Entities;

public class LocationGroup
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<LocationGroupItem> Items { get; set; } = [];
}
