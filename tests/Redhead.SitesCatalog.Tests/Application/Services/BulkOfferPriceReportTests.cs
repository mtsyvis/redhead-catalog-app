using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Controllers;
using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Application.Services;

public sealed class BulkOfferPriceReportTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DateTime OperationTime = new(2026, 11, 12, 8, 15, 0, DateTimeKind.Utc);
    public void Dispose() => _context.Dispose();
    private BulkOfferPriceOperationService CreateService() => new(_context, new(_context, new(_context)));

    [Theory]
    [InlineData("No term", "No term → 1 year", "Term corrected")]
    [InlineData("1 year", "1 year", "")]
    public async Task UpdateReport_NewPriceRetainsOfferTermCorrection(string oldTerm, string term, string note)
    {
        // Arrange
        var id = Guid.NewGuid();
        var value = Value(100);
        var row = Row("empty.example", id, "Ready", new(WebmasterOfferPriceType.Main, null, value, true)) with { CurrentTerm = oldTerm };
        var snapshot = CreateSnapshot([row], [new(id, [], [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), value, null)])], []);
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, false))!);
        var saved = Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "Changes"));

        // Assert
        Assert.Equal(term, saved["Term"]);
        Assert.Equal(note, saved["Note"]);
        Assert.Equal("100", saved["After USD"]);
    }

    [Theory]
    [InlineData(false, "multiline")]
    [InlineData(true, "multiline")]
    [InlineData(false, "long")]
    [InlineData(true, "long")]
    [InlineData(false, "maximum")]
    [InlineData(true, "maximum")]
    public async Task Reports_KeepLongContactsReadableWithinExcelRowHeight(bool calculation, string kind)
    {
        // Arrange
        var id = Guid.NewGuid();
        var value = Value(100);
        var row = Row("header.example", id, "Ready", new(WebmasterOfferPriceType.Main, Value(50), value, true));
        var snapshot = CreateSnapshot([row], [new(id, [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), Value(50), null)],
            [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), value, null)])],
            [new(row.Domain, PriceType.Main, PricingTerm.FiniteYears(1), 50, 100, false)]);
        var contact = kind switch { "multiline" => "Contact instructions\nTelegram: @supplier\nAvailable Monday-Friday", "long" => new string('W', 300), _ => new string('W', 20000) };
        snapshot = snapshot with { Operation = snapshot.Operation with { Result = snapshot.Operation.Result with { PrimaryEmail = contact } } };
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, calculation))!);
        var name = calculation ? "Webmaster costs" : "Changes";
        var sheet = XlsxTestWorkbook.ReadWorksheet(workbook, name);
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var headerRow = int.Parse(new string(sheet.Root!.Element(ns + "autoFilter")!.Attribute("ref")!.Value.Split(':')[0].Where(char.IsDigit).ToArray()));
        var intro = sheet.Descendants(ns + "row").Take(headerRow - 1).ToArray();
        var text = string.Concat(intro.SelectMany(row => row.Descendants(ns + "t")).Select(text => text.Value));

        // Assert
        Assert.Contains(contact.Replace("\n", ""), text.Replace("\n", ""));
        Assert.Contains("Term: 1 year", text.Replace("\n", ""));
        Assert.All(intro, line =>
        {
            var height = double.Parse(line.Attribute("ht")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(height, 8, 409.5);
            var content = string.Concat(line.Descendants(ns + "t").Select(t => t.Value));
            if (content.Length > 0) Assert.True(height >= 18 * (content.Count(ch => ch == '\n') + 1));
        });
        Assert.True(double.Parse(intro[1].Attribute("ht")!.Value, System.Globalization.CultureInfo.InvariantCulture) > 18);
        Assert.Equal($"A{headerRow + 1}", sheet.Descendants(ns + "pane").Single().Attribute("topLeftCell")!.Value);
        Assert.Single(XlsxTestWorkbook.ReadRows(workbook, name));
    }

    [Fact]
    public async Task Calculation_MissingReasonsStayMatchedAcrossManyDomainsAndServices()
    {
        // Arrange
        var rows = Enumerable.Range(0, 1000).Select(i => Row($"domain-{i:D4}.example", Guid.NewGuid(), "Ready",
            new(WebmasterOfferPriceType.Casino, Value(50), new(ServiceAvailabilityStatus.NotAvailable, null, "USD", null, null, null), true))
            with { CurrentTerm = i % 2 == 0 ? "No term" : "1 year" }).ToArray();
        var minima = rows.SelectMany(row => new[] { PriceType.Casino, PriceType.Crypto, PriceType.Dating }.Select(type =>
            new BulkOfferPurchaseMinimum(row.Domain, type, row.CurrentTerm == "No term" ? PricingTerm.Unknown : PricingTerm.FiniteYears(1), 50, null, false))).ToArray();
        var snapshot = CreateSnapshot(rows, [], minima);
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, true))!);
        var saved = XlsxTestWorkbook.ReadRows(workbook, "Needs review");

        // Assert
        Assert.Equal(3000, saved.Count);
        Assert.All(saved, line => Assert.Equal(line["Term"] == "No term"
            ? "Term corrected to 1 year; no webmaster price remains for No term."
            : "No webmaster price is available for this service and term. Review the catalog price manually.", line["Reason"]));
    }

    [Fact]
    public async Task UpdateReport_IsCompactAndComplete_WithCorrectionsAndReadableExceptions()
    {
        // Arrange
        var snapshot = Snapshot();
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, false))!);
        var changes = XlsxTestWorkbook.ReadRows(workbook, "Changes");

        // Assert
        Assert.Equal(new[] { "Changes", "No changes", "Exceptions" }, XlsxTestWorkbook.GetSheetNames(workbook));
        Assert.Equal(new[] { "Domain", "Service", "Term", "Before USD", "After USD", "Note" }, XlsxTestWorkbook.ReadHeaders(workbook, "Changes"));
        Assert.Equal(40, changes.Count);
        Assert.Equal("No term → 1 year", changes[0]["Term"]);
        Assert.Contains("Term corrected", changes[0]["Note"]);
        Assert.Contains("Offer reactivated", changes[0]["Note"]);
        Assert.Equal("40", changes[1]["Before USD"]);
        Assert.Equal("40", changes[1]["After USD"]);
        Assert.Equal("85", changes[0]["After USD"]);
        Assert.Equal("n", XlsxTestWorkbook.ReadCellType(workbook, "Changes", "E6"));
        Assert.Equal("inlineStr", XlsxTestWorkbook.ReadCellType(workbook, "Changes", "A6"));
        Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "No changes"));
        Assert.Equal(new[] { "Domain" }, XlsxTestWorkbook.ReadHeaders(workbook, "No changes"));
        var exception = Assert.Single(XlsxTestWorkbook.ReadRows(workbook, "Exceptions"));
        Assert.Equal("99", exception["Source row"]);
        Assert.Equal("bad input", exception["Domain"]);
        Assert.Equal("oops", exception["Main"]);
        Assert.Contains("invalid amount", exception["Reason"]);
        Assert.Equal("Price list update", XlsxTestWorkbook.ReadCell(workbook, "Changes", "A1"));
        Assert.Equal("Contact: contact@example.com · Term: 1 year", XlsxTestWorkbook.ReadCell(workbook, "Changes", "A2"));
        Assert.Equal("Updated: 12 Nov 2026, 08:15 UTC · By: admin@example.com", XlsxTestWorkbook.ReadCell(workbook, "Changes", "A3"));
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XlsxTestWorkbook.ReadWorksheet(workbook, "Changes");
        Assert.Equal(new[] { "A1:F1", "A2:F2", "A3:F3" }, sheet.Descendants(ns + "mergeCell").Select(cell => cell.Attribute("ref")!.Value));
        Assert.Equal("A5:F45", sheet.Root!.Element(ns + "autoFilter")!.Attribute("ref")!.Value);
        Assert.Equal("A6", sheet.Descendants(ns + "pane").Single().Attribute("topLeftCell")!.Value);
        Assert.Equal(new[] { "24", "18", "18", "8" }, sheet.Descendants(ns + "row").Take(4).Select(row => row.Attribute("ht")!.Value));
        Assert.Empty(sheet.Descendants(ns + "row").ElementAt(3).Elements());
        var title = XlsxTestWorkbook.ReadCellFont(workbook, "Changes", "A1");
        Assert.NotNull(title.Element(ns + "b"));
        Assert.Equal("14", title.Element(ns + "sz")!.Attribute("val")!.Value);
        Assert.Equal("10", XlsxTestWorkbook.ReadCellFont(workbook, "Changes", "A2").Element(ns + "sz")!.Attribute("val")!.Value);
        Assert.Null(XlsxTestWorkbook.ReadCellFillRgb(workbook, "Changes", "A1"));
    }

    [Theory]
    [InlineData(ServiceAvailabilityStatus.NotAvailable, "NO")]
    [InlineData(ServiceAvailabilityStatus.AvailableWithUnknownPrice, "YES")]
    public async Task UpdateReport_WritesAvailabilityAsReadableText(ServiceAvailabilityStatus status, string label)
    {
        // Arrange
        var snapshot = Snapshot();
        var offer = snapshot.Offers[0];
        var after = offer.After.Select(price => price.PriceType == WebmasterOfferPriceType.Casino
            ? price with { Value = new(status, null, "USD", null, null, null) } : price).ToArray();
        snapshot = snapshot with { Offers = snapshot.Offers.Select(saved => saved.OfferId == offer.OfferId ? saved with { After = after } : saved).ToArray() };
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, false))!);
        var rows = XlsxTestWorkbook.ReadRows(workbook, "Changes");

        // Assert
        Assert.Equal(label, rows[1]["After USD"]);
        Assert.Equal("inlineStr", XlsxTestWorkbook.ReadCellType(workbook, "Changes", "E7"));
        Assert.Equal("n", XlsxTestWorkbook.ReadCellType(workbook, "Changes", "D7"));
    }

    [Fact]
    public async Task Calculation_OnlyExportsAffectedColumns_WithTypedNumbersAndConditionalReviewSheet()
    {
        // Arrange
        var snapshot = Snapshot();
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, true))!);
        var numeric = XlsxTestWorkbook.ReadRows(workbook, "Webmaster costs");
        var missing = XlsxTestWorkbook.ReadRows(workbook, "Needs review");

        // Assert
        Assert.Equal(new[] { "Webmaster costs", "Needs review" }, XlsxTestWorkbook.GetSheetNames(workbook));
        Assert.Equal(2, numeric.Count);
        Assert.Equal("1 year", numeric[0]["Term"]);
        Assert.Equal("85", numeric[0]["Main cost USD"]);
        Assert.Equal("40", numeric[0]["Casino cost USD"]);
        Assert.False(numeric[0].ContainsKey("Crypto cost USD"));
        Assert.Equal("", numeric[0]["Link insertion casino cost USD"]);
        Assert.Equal("2 years", numeric[1]["Term"]);
        Assert.Equal("30", numeric[1]["Link insertion casino cost USD"]);
        Assert.All(numeric, row => Assert.False(row.ContainsKey("Quarantined")));
        Assert.All(missing, row => Assert.False(row.ContainsKey("Quarantined")));
        Assert.Equal("n", XlsxTestWorkbook.ReadCellType(workbook, "Webmaster costs", "C7"));
        Assert.Equal("blank", XlsxTestWorkbook.ReadCellType(workbook, "Webmaster costs", "E7"));
        Assert.Equal("Catalog calculation", XlsxTestWorkbook.ReadCell(workbook, "Webmaster costs", "A1"));
        Assert.Equal("Contact: contact@example.com · Term: 1 year", XlsxTestWorkbook.ReadCell(workbook, "Webmaster costs", "A2"));
        Assert.Equal("Updated: 12 Nov 2026, 08:15 UTC · By: admin@example.com", XlsxTestWorkbook.ReadCell(workbook, "Webmaster costs", "A3"));
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = XlsxTestWorkbook.ReadWorksheet(workbook, "Webmaster costs");
        Assert.Equal(new[] { "A1:E1", "A2:E2", "A3:E3", "A4:E4" }, sheet.Descendants(ns + "mergeCell").Select(cell => cell.Attribute("ref")!.Value));
        Assert.Equal("A6:E8", sheet.Root!.Element(ns + "autoFilter")!.Attribute("ref")!.Value);
        Assert.Equal("A7", sheet.Descendants(ns + "pane").Single().Attribute("topLeftCell")!.Value);
        Assert.True(double.Parse(sheet.Descendants(ns + "row").ElementAt(3).Attribute("ht")!.Value, System.Globalization.CultureInfo.InvariantCulture) >= 36);
        Assert.Equal(2, missing.Count);
        Assert.Contains(missing, row => row["Term"] == "No term" && row["Reason"].Contains("Term corrected to 1 year"));
        Assert.Contains(missing, row => row["Service"] == "Link insertion" && row["Reason"].Contains("no webmaster price", StringComparison.OrdinalIgnoreCase));
        var instructions = XlsxTestWorkbook.ReadCell(workbook, "Webmaster costs", "A4").Replace("\n", "");
        Assert.Contains("Filled cell: use this webmaster cost to calculate and update the catalog price for this service and term.", instructions);
        Assert.Contains("Blank cell: keep the current catalog price unchanged.", instructions);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Calculation_OpensOnTheOnlyUsefulSheet_WithoutEmptyColumnsOrQuarantine(bool numeric)
    {
        // Arrange
        var snapshot = Snapshot() with { Minima = [new("changed.example", PriceType.Main, PricingTerm.FiniteYears(1), 50, numeric ? 60 : null, false)] };
        await SaveAsync(snapshot);

        // Act
        using var workbook = new MemoryStream((await CreateService().DownloadAsync(snapshot.Operation.Id, true))!);
        var name = numeric ? "Webmaster costs" : "Needs review";
        var rows = XlsxTestWorkbook.ReadRows(workbook, name);

        // Assert
        Assert.Equal(new[] { name }, XlsxTestWorkbook.GetSheetNames(workbook));
        var row = Assert.Single(rows);
        Assert.False(row.ContainsKey("Quarantined"));
        Assert.Equal("Catalog calculation", XlsxTestWorkbook.ReadCell(workbook, name, "A1"));
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal("14", XlsxTestWorkbook.ReadCellFont(workbook, name, "A1").Element(ns + "sz")!.Attribute("val")!.Value);
        Assert.Equal("10", XlsxTestWorkbook.ReadCellFont(workbook, name, "A2").Element(ns + "sz")!.Attribute("val")!.Value);
        Assert.Equal($"A1:{(numeric ? "C" : "D")}1", XlsxTestWorkbook.ReadWorksheet(workbook, name).Descendants(ns + "mergeCell").First().Attribute("ref")!.Value);
        Assert.Contains(numeric ? "Blank cell: keep the current catalog price unchanged." : "Do not automatically set them to zero or NO.", XlsxTestWorkbook.ReadCell(workbook, name, "A4").Replace("\n", ""));
        if (numeric)
        {
            Assert.Equal("60", row["Main cost USD"]);
            Assert.Equal(3, row.Count);
        }
        else
        {
            Assert.Contains("No webmaster price", row["Reason"]);
            Assert.Equal(4, row.Count);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedResults_ReadLegacyMinimaButSkipUnneededSnapshotSections(bool legacy)
    {
        // Arrange
        var snapshot = Snapshot();
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshot, JsonOptions))!;
        json["input"] = "must not deserialize";
        json["offers"] = "must not deserialize";
        if (!legacy)
        {
            var minima = snapshot.Minima.ToLookup(key => key.Domain);
            var result = snapshot.Operation.Result with { Rows = snapshot.Operation.Result.Rows.Select(row => row with
                { PurchaseMinima = minima[row.Domain].ToArray() }).ToArray() };
            json["operation"] = JsonSerializer.SerializeToNode(snapshot.Operation with { Result = result }, JsonOptions);
            json["minima"] = "must not deserialize when display data is already present";
        }
        await SaveAsync(snapshot, json.ToJsonString());

        // Act
        var resultRead = await CreateService().GetAsync(snapshot.Operation.Id);

        // Assert
        var row = resultRead!.Result.Rows[0];
        Assert.True(row.CatalogRecalculationNeeded);
        Assert.Contains(row.PurchaseMinima!, key => key.BeforeUsd == 50 && key.AfterUsd == null);
        Assert.Equal(OperationTime, resultRead.AppliedAtUtc);
        Assert.Empty(await _context.SiteWebmasterOffers.ToListAsync());
    }

    [Fact]
    public async Task Calculation_RejectsEmptyRecalculationAndAllowsUpdateReport_AndMissingIdsReturnNotFound()
    {
        // Arrange
        var snapshot = Snapshot() with { Minima = [new("changed.example", PriceType.Main, PricingTerm.Unknown, 70, 70, false)] };
        await SaveAsync(snapshot);
        var service = CreateService();
        var controller = new BulkOfferPricePreviewController(new(_context, new(_context)), service);

        // Act
        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.DownloadAsync(snapshot.Operation.Id, true));
        var update = await controller.UpdateReport(snapshot.Operation.Id, default);
        var absentUpdate = await controller.UpdateReport(Guid.NewGuid(), default);
        var absentCalculation = await controller.CatalogCalculation(Guid.NewGuid(), default);

        // Assert
        Assert.Contains("No purchase minima changed", error.Message);
        var file = Assert.IsType<FileContentResult>(update);
        Assert.EndsWith($"{snapshot.Operation.Id:D}.xlsx", file.FileDownloadName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.IsType<NotFoundResult>(absentUpdate);
        Assert.IsType<NotFoundResult>(absentCalculation);
    }

    private async Task SaveAsync(BulkOfferPriceOperationSnapshot snapshot, string? json = null)
    {
        _context.BulkOfferPriceOperations.Add(new() { Id = snapshot.Operation.Id, Actor = snapshot.Operation.Actor,
            AppliedAtUtc = snapshot.Operation.AppliedAtUtc, PrimaryEmail = snapshot.Operation.Result.PrimaryEmail,
            RequestHash = "test", SnapshotJson = json ?? JsonSerializer.Serialize(snapshot, JsonOptions) });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private static BulkOfferPriceOperationSnapshot CreateSnapshot(IReadOnlyList<BulkOfferPricePreviewRow> rows,
        IReadOnlyList<BulkOfferSavedOffer> offers, IReadOnlyList<BulkOfferPurchaseMinimum> minima)
        => new(new(Guid.NewGuid(), "admin@example.com", OperationTime, [WebmasterOfferPriceType.Main],
            new("contact@example.com", "1 year", "USD", rows.Count, 0, rows.Count, 0, 0, rows, null)),
            new("contact@example.com", TermType.Finite, 1, TermUnit.Year, "USD", [WebmasterOfferPriceType.Main], rows.SelectMany(row => row.Inputs).ToArray()), offers, minima);

    private static BulkOfferPriceOperationSnapshot Snapshot()
    {
        var id = Guid.NewGuid();
        var old = Value(50);
        var next = new BulkOfferPriceValue(ServiceAvailabilityStatus.Available, 85, "CAD", 100, .8m, new(2026, 11, 10), 5);
        var main = new BulkOfferPriceChange(WebmasterOfferPriceType.Main, old, next, true);
        var updated = Row("changed.example", id, "Ready", main) with { CurrentTerm = "No term", ProposedTerm = "1 year",
            CurrentStatus = SiteWebmasterOfferStatus.Inactive, Inputs = [new(2, "changed.example", ["100"]), new(3, "changed.example", ["100"])] };
        var rows = new List<BulkOfferPricePreviewRow> { updated };
        var offers = new List<BulkOfferSavedOffer> { new(id,
            [new(WebmasterOfferPriceType.Main, PricingTerm.Unknown, old, null), new(WebmasterOfferPriceType.Casino, PricingTerm.Unknown, Value(40), null)],
            [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), next, null), new(WebmasterOfferPriceType.Casino, PricingTerm.FiniteYears(1), Value(40), null)]) };
        // 38 additional domains prove that exports are not limited to the visible UI page.
        for (var i = 0; i < 38; i++)
        {
            var offerId = Guid.NewGuid();
            rows.Add(Row($"page-{i}.example", offerId, "Ready", main));
            offers.Add(new(offerId, [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), old, null)],
                [new(WebmasterOfferPriceType.Main, PricingTerm.FiniteYears(1), next, null)]));
        }
        rows.Add(Row("noop.example", Guid.NewGuid(), "No changes", main with { Proposed = old, Changed = false }));
        rows.Add(Row("bad input", null, "Exceptions", main) with { Inputs = [new(99, "bad input", ["oops"])], Prices = [],
            Category = "Invalid selected value", Reasons = ["invalid amount"] });
        var result = new BulkOfferPricePreviewResult("contact@example.com", "1 year", "CAD", 42, 1, 39, 1, 1, rows, null);
        return new(new(Guid.NewGuid(), "admin@example.com", OperationTime, [WebmasterOfferPriceType.Main], result),
            new("contact@example.com", TermType.Finite, 1, TermUnit.Year, "CAD", [WebmasterOfferPriceType.Main], rows.SelectMany(row => row.Inputs).ToArray()), offers,
            [new("changed.example", PriceType.Main, PricingTerm.Unknown, 50, null, true),
             new("changed.example", PriceType.Main, PricingTerm.FiniteYears(1), null, 85, true),
             new("changed.example", PriceType.Casino, PricingTerm.Unknown, null, null, true),
             new("changed.example", PriceType.Casino, PricingTerm.FiniteYears(1), null, 40, true),
             new("changed.example", PriceType.Crypto, PricingTerm.FiniteYears(1), 7, 7, true),
             new("changed.example", PriceType.LinkInsertionCasino, PricingTerm.FiniteYears(2), null, 30, true),
             new("changed.example", PriceType.LinkInsertion, PricingTerm.FiniteYears(1), 20, null, true)]);
    }
    private static BulkOfferPriceValue Value(decimal value) => new(ServiceAvailabilityStatus.Available, value, "USD", value, 1, null);
    private static BulkOfferPricePreviewRow Row(string domain, Guid? id, string outcome, BulkOfferPriceChange change)
        => new(domain, false, [new(1, domain, ["100"])], outcome, null, [], id, "1 year", "1 year",
            SiteWebmasterOfferStatus.Active, SiteWebmasterOfferStatus.Active, [change]);
}
