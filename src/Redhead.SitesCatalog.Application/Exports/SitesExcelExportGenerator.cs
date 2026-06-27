using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Application.Services.Analytics;

namespace Redhead.SitesCatalog.Application.Exports;

public sealed class SitesExcelExportGenerator : ISitesExcelExportGenerator
{
    public MemoryStream Generate(SitesExcelExportRequest request)
    {
        var siteColumns = SitesExportColumnRegistry.ValidateRequestedColumns(
            request.VisibleColumnKeys,
            request.ExportColumnRole);

        var sheets = new List<XlsxSheet>
        {
            new(
                "Sites",
                siteColumns.Select(column => column.Header).ToArray(),
                request.Sites.Select(site => CreateSiteRow(
                    site,
                    siteColumns,
                    request.SelectedTermKey,
                    request.PriceCellMode)).ToList(),
                siteColumns.Select(column => column.Width).ToArray())
        };

        if (request.NotFoundDomains.Count > 0)
        {
            sheets.Add(new XlsxSheet(
                "Not found",
                ["Domain"],
                request.NotFoundDomains.Select(domain => (IReadOnlyList<XlsxCell>)[XlsxCell.Text(domain)]).ToList(),
                [32d]));
        }

        sheets.Add(
            new(
                "Export info",
                ["Property", "Value"],
                CreateExportInfoRows(
                    request.GeneratedBy,
                    request.RoleLabel,
                    request.RequestedRows,
                    request.ExportedRows,
                    request.Truncated,
                    request.LimitRows,
                    request.SelectedTermKey,
                    request.PriceCellMode,
                    request.NotFoundDomains.Count,
                    request.NotFoundIncluded,
                    request.TruncationReason),
                [28d, 80d],
                FreezeHeader: false,
                AutoFilter: false));

        return XlsxWorkbookWriter.CreateWorkbook(sheets);
    }

    private static IReadOnlyList<XlsxCell> CreateSiteRow(
        Site site,
        IReadOnlyList<SitesExportColumnDefinition> siteColumns,
        string? selectedTermKey,
        SitesExcelPriceCellMode priceCellMode)
        => siteColumns.Select(column => column.CreateCell(site, selectedTermKey, priceCellMode)).ToList();

    private static IReadOnlyList<IReadOnlyList<XlsxCell>> CreateExportInfoRows(
        string generatedBy,
        string roleLabel,
        int requestedRows,
        int exportedRows,
        bool truncated,
        int? limitRows,
        string? selectedTermKey,
        SitesExcelPriceCellMode priceCellMode,
        int notFoundRows,
        bool notFoundIncluded,
        string? truncationReason)
    {
        var rows = new List<IReadOnlyList<XlsxCell>>
        {
            InfoRow("GeneratedAtUtc", XlsxCell.DateTime(DateTime.UtcNow)),
            InfoRow("GeneratedBy", XlsxCell.Text(generatedBy)),
            InfoRow("Role", XlsxCell.Text(roleLabel)),
            InfoRow("Rows matching export request", XlsxCell.Number(requestedRows, XlsxCellStyle.Integer)),
            InfoRow("Rows in Sites sheet", XlsxCell.Number(exportedRows, XlsxCellStyle.Integer)),
            InfoRow("Export truncated by limit", XlsxCell.Boolean(truncated)),
            InfoRow("Export limit rows", limitRows.HasValue
                ? XlsxCell.Number(limitRows.Value, XlsxCellStyle.Integer)
                : XlsxCell.Text("Unlimited")),
            InfoRow("Term pricing", XlsxCell.Text(FormatTermPricingNote(selectedTermKey, priceCellMode)))
        };

        if (notFoundRows > 0)
        {
            rows.Add(InfoRow("Not found sheet rows", XlsxCell.Number(notFoundRows, XlsxCellStyle.Integer)));
            rows.Add(InfoRow("Not found included", XlsxCell.Text(notFoundIncluded ? "Yes" : "No")));
        }

        if (!string.IsNullOrWhiteSpace(truncationReason))
        {
            rows.Add(InfoRow("Export truncation reason", XlsxCell.Text(truncationReason)));
        }

        return rows;
    }

    private static IReadOnlyList<XlsxCell> InfoRow(string label, XlsxCell value)
        => [XlsxCell.InfoLabel(label), value];

    private static string FormatTermPricingNote(
        string? selectedTermKey,
        SitesExcelPriceCellMode priceCellMode)
    {
        if (priceCellMode == SitesExcelPriceCellMode.AllTerms)
        {
            return "All term-specific prices are included in each price column.";
        }

        if (string.IsNullOrWhiteSpace(selectedTermKey))
        {
            return "Term is not applied. Selected minimum available price for each price column.";
        }

        var label = AnalyticsTermLabelFormatter.FormatTermKey(selectedTermKey);
        return $"Term applied: {label}. Selected prices for {label} only.";
    }
}
