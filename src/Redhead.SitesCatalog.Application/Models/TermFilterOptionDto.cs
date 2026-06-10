using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models;

public sealed class TermFilterOptionDto
{
    public string TermKey { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public TermType? TermType { get; init; }
    public int? TermValue { get; init; }
    public TermUnit? TermUnit { get; init; }
}
