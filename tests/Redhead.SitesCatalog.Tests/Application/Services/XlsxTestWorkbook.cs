using System.IO.Compression;
using System.Xml.Linq;

namespace Redhead.SitesCatalog.Tests;

internal static class XlsxTestWorkbook
{
    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static List<string> GetSheetNames(Stream stream)
    {
        using var archive = OpenArchive(stream);
        var workbook = ReadXml(archive, "xl/workbook.xml");
        return workbook
            .Root!
            .Element(SpreadsheetNs + "sheets")!
            .Elements(SpreadsheetNs + "sheet")
            .Select(sheet => sheet.Attribute("name")!.Value)
            .ToList();
    }

    public static List<Dictionary<string, string>> ReadRows(Stream stream, string sheetName)
    {
        using var archive = OpenArchive(stream);
        var sheetPath = ResolveSheetPath(archive, sheetName);
        var worksheet = ReadXml(archive, sheetPath);
        var sheetRows = worksheet
            .Root!
            .Element(SpreadsheetNs + "sheetData")!
            .Elements(SpreadsheetNs + "row")
            .ToList();

        if (sheetRows.Count == 0)
        {
            return [];
        }

        var headers = ReadRowValues(sheetRows[0]);
        var rows = new List<Dictionary<string, string>>();
        foreach (var rowElement in sheetRows.Skip(1))
        {
            var values = ReadRowValues(rowElement);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                row[headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    public static List<string> ReadHeaders(Stream stream, string sheetName)
    {
        using var archive = OpenArchive(stream);
        var sheetPath = ResolveSheetPath(archive, sheetName);
        var worksheet = ReadXml(archive, sheetPath);
        var headerRow = worksheet
            .Root!
            .Element(SpreadsheetNs + "sheetData")!
            .Elements(SpreadsheetNs + "row")
            .First();

        return ReadRowValues(headerRow);
    }

    private static ZipArchive OpenArchive(Stream stream)
    {
        stream.Position = 0;
        return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
    }

    private static string ResolveSheetPath(ZipArchive archive, string sheetName)
    {
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var sheet = workbook
            .Root!
            .Element(SpreadsheetNs + "sheets")!
            .Elements(SpreadsheetNs + "sheet")
            .Single(s => s.Attribute("name")!.Value == sheetName);

        var relationshipId = sheet.Attribute(RelationshipNs + "id")!.Value;
        var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels");
        var target = relationships
            .Root!
            .Elements(PackageRelationshipNs + "Relationship")
            .Single(rel => rel.Attribute("Id")!.Value == relationshipId)
            .Attribute("Target")!
            .Value;

        return $"xl/{target}";
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidOperationException($"Missing XLSX entry: {path}");
        using var entryStream = entry.Open();
        return XDocument.Load(entryStream);
    }

    private static List<string> ReadRowValues(XElement row)
    {
        var values = new SortedDictionary<int, string>();
        foreach (var cell in row.Elements(SpreadsheetNs + "c"))
        {
            var reference = cell.Attribute("r")?.Value ?? string.Empty;
            var column = ParseColumnIndex(reference);
            values[column] = ReadCellValue(cell);
        }

        if (values.Count == 0)
        {
            return [];
        }

        var result = new List<string>();
        for (var i = 1; i <= values.Keys.Max(); i++)
        {
            result.Add(values.TryGetValue(i, out var value) ? value : string.Empty);
        }

        return result;
    }

    private static string ReadCellValue(XElement cell)
    {
        var type = cell.Attribute("t")?.Value;
        return type switch
        {
            "inlineStr" => string.Concat(cell
                .Element(SpreadsheetNs + "is")!
                .Elements(SpreadsheetNs + "t")
                .Select(t => t.Value)),
            "b" => cell.Element(SpreadsheetNs + "v")?.Value == "1" ? "TRUE" : "FALSE",
            _ => cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty
        };
    }

    private static int ParseColumnIndex(string cellReference)
    {
        var total = 0;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            total = total * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }

        return total;
    }
}
