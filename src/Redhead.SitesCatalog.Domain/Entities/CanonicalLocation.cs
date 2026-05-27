namespace Redhead.SitesCatalog.Domain.Entities;

public class CanonicalLocation
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }

    public ICollection<LocationGroupItem> GroupItems { get; set; } = [];
    public ICollection<Site> Sites { get; set; } = [];
}
