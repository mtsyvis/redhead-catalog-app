using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Redhead.SitesCatalog.Application.Exports;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class WebmasterOffersExcelExportTests
{
    [Fact]
    public void Generate_UsesReadableHeadersAndCurrentOfferValuesWithPrimaryEmailLast()
    {
        // Arrange
        var offer = new WebmasterOfferDto
        {
            ContactRawText = "Original contact\nSecond line", PrimaryEmail = "current@publisher.example",
            OutreachSenderRawText = "Sender", LinkbuilderMailboxRawText = "Imported mailbox",
            LinkPolicyText = "Dofollow", DfLinksRawText = "2", SponsoredTagRawText = "No tag",
            CommentText = "=literal text", ClientRawText = "Client", TermRawText = "24 months", TermLabel = "2 years",
            Prices =
            [
                Price(WebmasterOfferPriceType.Main, ServiceAvailabilityStatus.Available, 125.50m, "Edited price"),
                Price(WebmasterOfferPriceType.Casino, ServiceAvailabilityStatus.AvailableWithUnknownPrice),
                Price(WebmasterOfferPriceType.Crypto, ServiceAvailabilityStatus.NotAvailable),
                Price(WebmasterOfferPriceType.Dating, ServiceAvailabilityStatus.Unknown, details: "Ask webmaster")
            ]
        };
        var result = Result(new WebmasterOffersDomainResult<WebmasterOfferDto> { Domain = "example.org", Offers = [offer] });

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var headers = XlsxTestWorkbook.ReadHeaders(stream, "Webmaster Offers");
        var rows = XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers");

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal([
            "Domain",
            "MainWebmasterPriceDetails", "MainWebmasterPriceUsd",
            "CasinoWebmasterPriceDetails", "CasinoWebmasterPriceUsd",
            "CryptoWebmasterPriceDetails", "CryptoWebmasterPriceUsd",
            "DatingWebmasterPriceDetails", "DatingWebmasterPriceUsd",
            "LinkInsertionWebmasterPriceDetails", "LinkInsertionWebmasterPriceUsd",
            "LinkInsertion18PlusWebmasterPriceDetails", "LinkInsertion18PlusWebmasterPriceUsd",
            "BannerWebmasterPriceDetails", "BannerWebmasterPriceUsd",
            "Banner18PlusWebmasterPriceDetails", "Banner18PlusWebmasterPriceUsd",
            "HomepageTextLinkWebmasterPriceDetails", "HomepageTextLinkWebmasterPriceUsd",
            "HomepageTextLink18PlusWebmasterPriceDetails", "HomepageTextLink18PlusWebmasterPriceUsd",
            "LinkPolicyText", "DfLinks", "SponsoredTag", "Term", "LinkbuilderMailbox", "LinkbuilderMailboxParsed",
            "OutreachSender", "Contact", "CommentText", "Client", "PrimaryEmail"
        ], headers);
        Assert.Equal("example.org", row["Domain"]);
        Assert.Equal("current@publisher.example", row["PrimaryEmail"]);
        Assert.Equal(offer.ContactRawText, row["Contact"]);
        Assert.Equal(offer.OutreachSenderRawText, row["OutreachSender"]);
        Assert.Equal(offer.LinkbuilderMailboxRawText, row["LinkbuilderMailbox"]);
        Assert.Equal("", row["LinkbuilderMailboxParsed"]);
        Assert.Equal(offer.LinkPolicyText, row["LinkPolicyText"]);
        Assert.Equal(offer.DfLinksRawText, row["DfLinks"]);
        Assert.Equal(offer.SponsoredTagRawText, row["SponsoredTag"]);
        Assert.Equal(offer.CommentText, row["CommentText"]);
        Assert.Equal(offer.ClientRawText, row["Client"]);
        Assert.Equal("2 years", row["Term"]);
        Assert.Equal("125.50", row["MainWebmasterPriceUsd"]);
        Assert.Equal("Edited price", row["MainWebmasterPriceDetails"]);
        Assert.Equal("YES", row["CasinoWebmasterPriceUsd"]);
        Assert.Equal("NO", row["CryptoWebmasterPriceUsd"]);
        Assert.Equal("", row["DatingWebmasterPriceUsd"]);
        Assert.Equal("Ask webmaster", row["DatingWebmasterPriceDetails"]);
        Assert.Equal("", row["BannerWebmasterPriceUsd"]);
        Assert.Equal(["Webmaster Offers"], XlsxTestWorkbook.GetSheetNames(stream));
    }

    [Fact]
    public void Generate_ExportsResolvedMailboxEmailsOnSeparateLines()
    {
        // Arrange
        var offer = new WebmasterOfferDto
        {
            LinkbuilderMailboxRawText = "Alice alias; Bob alias",
            LinkbuilderMailboxes =
            [
                new WebmasterOfferMailboxDto { Email = "alice@example.org", DisplayName = "Alice", Source = LinkbuilderMailboxOfferSource.Import },
                new WebmasterOfferMailboxDto { Email = "bob@example.org", DisplayName = "Bob", Source = LinkbuilderMailboxOfferSource.Manual }
            ]
        };
        var result = Result(new WebmasterOffersDomainResult<WebmasterOfferDto> { Domain = "example.org", Offers = [offer] });

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var rows = XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers");

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal("Alice alias; Bob alias", row["LinkbuilderMailbox"]);
        Assert.Equal("alice@example.org\nbob@example.org", row["LinkbuilderMailboxParsed"]);
    }

    [Theory]
    [InlineData(WebmasterOfferPriceType.Main)]
    [InlineData(WebmasterOfferPriceType.Casino)]
    [InlineData(WebmasterOfferPriceType.Crypto)]
    [InlineData(WebmasterOfferPriceType.Dating)]
    [InlineData(WebmasterOfferPriceType.LinkInsertion)]
    [InlineData(WebmasterOfferPriceType.LinkInsertion18Plus)]
    [InlineData(WebmasterOfferPriceType.Banner)]
    [InlineData(WebmasterOfferPriceType.Banner18Plus)]
    [InlineData(WebmasterOfferPriceType.HomepageTextLink)]
    [InlineData(WebmasterOfferPriceType.HomepageTextLink18Plus)]
    public void Generate_MapsAllTenPriceTypes(WebmasterOfferPriceType priceType)
    {
        // Arrange
        var offer = new WebmasterOfferDto
        {
            Prices = [Price(priceType, ServiceAvailabilityStatus.Available, 75m, "Conditions")]
        };
        var result = Result(new WebmasterOffersDomainResult<WebmasterOfferDto> { Domain = "example.org", Offers = [offer] });

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var rows = XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers");

        // Assert
        var row = Assert.Single(rows);
        Assert.Equal("75", row[$"{priceType}WebmasterPriceUsd"]);
        Assert.Equal("Conditions", row[$"{priceType}WebmasterPriceDetails"]);
    }

    [Fact]
    public void Generate_ExportsEveryOfferInDomainOrderAndStylesEntireInactiveRows()
    {
        // Arrange
        var result = Result(
            new WebmasterOffersDomainResult<WebmasterOfferDto>
            {
                Domain = "second.example", Offers = [new WebmasterOfferDto { Status = SiteWebmasterOfferStatus.Inactive }]
            },
            new WebmasterOffersDomainResult<WebmasterOfferDto>
            {
                Domain = "first.example",
                Offers = Enumerable.Range(0, 105).Select(index => new WebmasterOfferDto
                {
                    Status = SiteWebmasterOfferStatus.Active, PrimaryEmail = $"contact{index}@example.org"
                }).ToList()
            });

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var rows = XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers");

        // Assert
        Assert.Equal(106, rows.Count);
        Assert.Equal("second.example", rows[0]["Domain"]);
        Assert.All(rows.Skip(1), row => Assert.Equal("first.example", row["Domain"]));
        Assert.Equal("contact104@example.org", rows[^1]["PrimaryEmail"]);
        foreach (var column in Enumerable.Range(0, rows[0].Count))
        {
            var name = column < 26 ? ((char)('A' + column)).ToString() : $"A{(char)('A' + column - 26)}";
            Assert.Equal("FFFBEEEE", XlsxTestWorkbook.ReadCellFillRgb(stream, "Webmaster Offers", $"{name}2"));
        }
        Assert.Null(XlsxTestWorkbook.ReadCellFillRgb(stream, "Webmaster Offers", "A3"));
    }

    [Fact]
    public void Generate_SizesEveryColumnToFitItsHeader()
    {
        // Arrange
        var result = Result(new WebmasterOffersDomainResult<WebmasterOfferDto>
        {
            Domain = "example.org", Offers = [new WebmasterOfferDto()]
        });
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var headers = XlsxTestWorkbook.ReadHeaders(stream, "Webmaster Offers");
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var columns = worksheet.Root!.Element(spreadsheet + "cols")!.Elements(spreadsheet + "col").ToList();

        // Assert
        Assert.Equal(headers.Count, columns.Count);
        foreach (var (header, column) in headers.Zip(columns))
        {
            var width = double.Parse(column.Attribute("width")!.Value, CultureInfo.InvariantCulture);
            Assert.True(width >= header.Length + 2, $"Column {header} is too narrow: {width}.");
            Assert.Equal("1", column.Attribute("customWidth")!.Value);
        }
    }

    [Theory]
    [InlineData(SiteWebmasterOfferStatus.Active)]
    [InlineData(SiteWebmasterOfferStatus.Inactive)]
    public void Generate_WrapsCompleteTextInFixedHeightRowsWithTopAlignment(SiteWebmasterOfferStatus status)
    {
        // Arrange
        var longText = string.Join('\n', Enumerable.Range(1, 20).Select(index => $"Line {index}: long offer conditions and contact notes."));
        var offer = new WebmasterOfferDto
        {
            Status = status, ContactRawText = longText, CommentText = longText,
            Prices = [Price(WebmasterOfferPriceType.Main, ServiceAvailabilityStatus.Available, 125.50m, longText)]
        };
        var result = Result(new WebmasterOffersDomainResult<WebmasterOfferDto> { Domain = "example.org", Offers = [offer] });
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        // Act
        using var stream = WebmasterOffersExcelExportGenerator.Generate(result);
        var exportedRows = XlsxTestWorkbook.ReadRows(stream, "Webmaster Offers");
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        using var worksheetStream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        using var stylesStream = archive.GetEntry("xl/styles.xml")!.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var styles = XDocument.Load(stylesStream).Root!.Element(spreadsheet + "cellXfs")!.Elements().ToList();
        var rows = worksheet.Root!.Element(spreadsheet + "sheetData")!.Elements(spreadsheet + "row").ToList();

        // Assert
        var exported = Assert.Single(exportedRows);
        Assert.Equal(longText, exported["Contact"]);
        Assert.Equal(longText, exported["CommentText"]);
        Assert.Equal(longText, exported["MainWebmasterPriceDetails"]);
        Assert.Equal("125.50", exported["MainWebmasterPriceUsd"]);
        Assert.Null(rows[0].Attribute("ht"));
        Assert.Equal("75", rows[1].Attribute("ht")!.Value);
        Assert.Equal("1", rows[1].Attribute("customHeight")!.Value);
        foreach (var cell in rows[1].Elements(spreadsheet + "c"))
        {
            var style = styles[int.Parse(cell.Attribute("s")!.Value, CultureInfo.InvariantCulture)];
            var alignment = style.Element(spreadsheet + "alignment");
            Assert.Equal("top", alignment?.Attribute("vertical")?.Value);
            Assert.Equal("1", alignment?.Attribute("wrapText")?.Value);
            Assert.Equal("1", style.Attribute("applyAlignment")?.Value);
            Assert.Equal(status == SiteWebmasterOfferStatus.Inactive ? "3" : "0", style.Attribute("fillId")!.Value);
        }
        var amountCell = rows[1].Elements(spreadsheet + "c").Single(cell => cell.Attribute("r")!.Value == "C2");
        var amountStyle = styles[int.Parse(amountCell.Attribute("s")!.Value, CultureInfo.InvariantCulture)];
        Assert.Equal("4", amountStyle.Attribute("numFmtId")!.Value);
    }

    private static WebmasterOffersMultiSearchResult<WebmasterOfferDto> Result(params WebmasterOffersDomainResult<WebmasterOfferDto>[] groups)
        => new() { Results = groups, DomainsWithoutOffers = ["empty.example"], NotFoundDomains = ["missing.example"] };

    private static WebmasterOfferPriceDto Price(WebmasterOfferPriceType type, ServiceAvailabilityStatus status,
        decimal? amount = null, string? details = null)
        => new() { PriceType = type, AvailabilityStatus = status, WebmasterPriceUsd = amount, WebmasterPriceDetails = details };
}
