using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Tests.Application.Exports;

public sealed class SitesExcelExportGeneratorTests
{
    [Theory]
    [InlineData("2026-11-10", "2026-11-10", false, false, "FFE2F0D9")]
    [InlineData("2026-11-10", "2025-11-10", false, false, "FFE2F0D9")]
    [InlineData("2026-11-10", "2025-11-09", false, false, null)]
    [InlineData("2026-11-10", "2026-11-11", false, false, null)]
    [InlineData("2026-11-10", null, false, false, null)]
    [InlineData("2026-11-10", "2026-11-01", true, false, "FFE2F0D9")]
    [InlineData("2026-11-10", "2025-11-01", true, false, null)]
    [InlineData("2026-11-10", "2026-06-15", false, true, "FFFBEEEE")]
    [InlineData("2024-02-29", "2023-02-28", false, false, "FFE2F0D9")]
    [InlineData("2024-02-29", "2023-02-27", false, false, null)]
    public void Generate_LastPublishedDate_AppliesExpectedRowFill(
        string exportDate,
        string? lastPublishedDate,
        bool monthOnly,
        bool quarantined,
        string? expectedFill)
    {
        // Arrange
        var site = new Site
        {
            Domain = "publication.example",
            LastPublishedDate = lastPublishedDate is null ? null : ParseDate(lastPublishedDate),
            LastPublishedDateIsMonthOnly = monthOnly,
            IsQuarantined = quarantined
        };
        var sut = new SitesExcelExportGenerator(new FixedClock(ParseDate(exportDate).AddHours(23)));
        var request = CreateRequest(site, ["domain", "dr", "trafficValueUsd", "niche"]);

        // Act
        using var workbook = sut.Generate(request);

        // Assert
        foreach (var column in new[] { "A", "B", "C", "D" })
        {
            Assert.Equal(expectedFill, XlsxTestWorkbook.ReadCellFillRgb(workbook, "Sites", $"{column}2"));
        }
        Assert.Equal("FFD9EAF7", XlsxTestWorkbook.ReadCellFillRgb(workbook, "Sites", "A1"));
        Assert.Null(XlsxTestWorkbook.ReadCellFillRgb(workbook, "Export info", "B2"));
    }

    [Theory]
    [InlineData(SitesExcelPriceCellMode.Contextual)]
    [InlineData(SitesExcelPriceCellMode.AllTerms)]
    public void Generate_RecentPublication_PreservesValuesFormatsAndOtherSheets(SitesExcelPriceCellMode priceCellMode)
    {
        // Arrange
        var site = new Site
        {
            Domain = "publication.example",
            DR = 42,
            TrafficValueUsd = 123.45m,
            LastPublishedDate = ParseDate("2026-06-15")
        };
        var sut = new SitesExcelExportGenerator(new FixedClock(ParseDate("2026-11-10")));
        var request = CreateRequest(site, ["domain", "dr", "trafficValueUsd", "niche", "lastPublishedDate"])
            with { NotFoundDomains = ["missing.example"], NotFoundIncluded = true, PriceCellMode = priceCellMode };
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        // Act
        using var workbook = sut.Generate(request);

        // Assert
        var row = Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "Sites"));
        Assert.Equal("publication.example", row["Domain"]);
        Assert.Equal("42", row["DR"]);
        Assert.Equal("123.45", row["Traffic Value USD"]);
        Assert.Equal(string.Empty, row["Niche"]);
        Assert.Equal("15.06.2026", row["Last Published"]);
        foreach (var column in new[] { "A", "B", "C", "D", "E" })
        {
            Assert.Equal("FFE2F0D9", XlsxTestWorkbook.ReadCellFillRgb(workbook, "Sites", $"{column}2"));
        }
        Assert.Null(XlsxTestWorkbook.ReadCellFillRgb(workbook, "Not found", "A2"));
        Assert.Equal("missing.example", Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "Not found"))["Domain"]);

        workbook.Position = 0;
        using var archive = new ZipArchive(workbook, ZipArchiveMode.Read);
        using var worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        using var stylesStream = archive.GetEntry("xl/styles.xml")!.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var styles = XDocument.Load(stylesStream).Root!.Element(spreadsheet + "cellXfs")!.Elements().ToList();
        var cells = worksheet.Root!.Element(spreadsheet + "sheetData")!.Elements(spreadsheet + "row")
            .ElementAt(1).Elements(spreadsheet + "c").ToList();
        Assert.Equal(new[] { "0", "3", "4", "0", "0" }, cells.Select(cell =>
            styles[int.Parse(cell.Attribute("s")!.Value)].Attribute("numFmtId")!.Value));
    }

    private static SitesExcelExportRequest CreateRequest(Site site, IReadOnlyList<string> columns)
        => new([site], [], columns, null, AppRoles.Admin, "export@example.com", AppRoles.Admin,
            RequestedRows: 1, ExportedRows: 1, Truncated: false, LimitRows: null, NotFoundIncluded: false);

    private static DateTime ParseDate(string date)
        => DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }
}
