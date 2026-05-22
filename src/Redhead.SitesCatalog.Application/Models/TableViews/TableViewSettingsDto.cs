namespace Redhead.SitesCatalog.Application.Models.TableViews;

public sealed class TableViewSettingsDto
{
    public int SchemaVersion { get; init; }
    public List<string> VisibleColumnIds { get; init; } = new();
    public string Density { get; init; } = "standard";
    public Dictionary<string, int> ColumnWidths { get; init; } = new();
}
