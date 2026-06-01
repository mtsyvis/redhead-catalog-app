using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Application.Services;

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
                request.Sites.Select(site => CreateSiteRow(site, siteColumns)).ToList(),
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
                    request.NotFoundDomains.Count,
                    request.NotFoundIncluded),
                [28d, 80d],
                FreezeHeader: false,
                AutoFilter: false));

        return XlsxWorkbookWriter.CreateWorkbook(sheets);
    }

    private static IReadOnlyList<XlsxCell> CreateSiteRow(
        Site site,
        IReadOnlyList<SitesExportColumnDefinition> siteColumns)
        => siteColumns.Select(column => column.CreateCell(site)).ToList();

    private static IReadOnlyList<IReadOnlyList<XlsxCell>> CreateExportInfoRows(
        string generatedBy,
        string roleLabel,
        int requestedRows,
        int exportedRows,
        bool truncated,
        int? limitRows,
        int notFoundRows,
        bool notFoundIncluded)
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
                : XlsxCell.Text("Unlimited"))
        };

        if (notFoundRows > 0)
        {
            rows.Add(InfoRow("Not found sheet rows", XlsxCell.Number(notFoundRows, XlsxCellStyle.Integer)));
            rows.Add(InfoRow("Not found included", XlsxCell.Text(notFoundIncluded ? "Yes" : "No")));
        }

        return rows;
    }

    private static IReadOnlyList<XlsxCell> InfoRow(string label, XlsxCell value)
        => [XlsxCell.InfoLabel(label), value];
}
