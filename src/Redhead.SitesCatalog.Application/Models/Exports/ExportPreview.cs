namespace Redhead.SitesCatalog.Application.Models.Exports;

public sealed record ExportPreview(int SelectionRows, int ExportableRows, int NotFoundRows, bool IsBlocked, string? Reason);
