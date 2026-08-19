namespace Redhead.SitesCatalog.Application.Models.ChangeHistory;

public sealed class EntityChangeHistoryDto
{
    public Guid Id { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string ChangedBy { get; init; } = string.Empty;
    public DateTime ChangedAtUtc { get; init; }
    public IReadOnlyList<EntityFieldChangeDto> Changes { get; init; } = [];
}

public sealed class EntityFieldChangeDto
{
    public string Field { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}
