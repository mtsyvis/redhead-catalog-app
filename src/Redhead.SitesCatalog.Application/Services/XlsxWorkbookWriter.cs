using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace Redhead.SitesCatalog.Application.Services;

internal static class XlsxWorkbookWriter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static MemoryStream CreateWorkbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteContentTypes(archive, sheets.Count);
            WriteRootRelationships(archive);
            WriteWorkbook(archive, sheets);
            WriteWorkbookRelationships(archive, sheets.Count);
            WriteStyles(archive);

            for (var i = 0; i < sheets.Count; i++)
            {
                WriteWorksheet(archive, i + 1, sheets[i]);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteContentTypes(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry("[Content_Types].xml").Open());
        writer.WriteStartDocument();
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "rels");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Default");
        writer.WriteAttributeString("Extension", "xml");
        writer.WriteAttributeString("ContentType", "application/xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/workbook.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        writer.WriteEndElement();
        writer.WriteStartElement("Override");
        writer.WriteAttributeString("PartName", "/xl/styles.xml");
        writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        writer.WriteEndElement();
        for (var i = 1; i <= sheetCount; i++)
        {
            writer.WriteStartElement("Override");
            writer.WriteAttributeString("PartName", $"/xl/worksheets/sheet{i}.xml");
            writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteRootRelationships(ZipArchive archive)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry("_rels/.rels").Open());
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", "rId1");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteAttributeString("Target", "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbook(ZipArchive archive, IReadOnlyList<XlsxSheet> sheets)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry("xl/workbook.xml").Open());
        writer.WriteStartDocument();
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipsNamespace);
        writer.WriteStartElement("sheets");
        for (var i = 0; i < sheets.Count; i++)
        {
            writer.WriteStartElement("sheet");
            writer.WriteAttributeString("name", sheets[i].Name);
            writer.WriteAttributeString("sheetId", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", RelationshipsNamespace, $"rId{i + 1}");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorkbookRelationships(ZipArchive archive, int sheetCount)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry("xl/_rels/workbook.xml.rels").Open());
        writer.WriteStartDocument();
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var i = 1; i <= sheetCount; i++)
        {
            writer.WriteStartElement("Relationship");
            writer.WriteAttributeString("Id", $"rId{i}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{i}.xml");
            writer.WriteEndElement();
        }
        writer.WriteStartElement("Relationship");
        writer.WriteAttributeString("Id", $"rId{sheetCount + 1}");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
        writer.WriteAttributeString("Target", "styles.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteStyles(ZipArchive archive)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry("xl/styles.xml").Open());
        writer.WriteStartDocument();
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);

        writer.WriteStartElement("numFmts");
        writer.WriteAttributeString("count", "2");
        WriteNumFmt(writer, "164", "yyyy-mm-dd");
        WriteNumFmt(writer, "165", "yyyy-mm-dd hh:mm");
        writer.WriteEndElement();

        writer.WriteStartElement("fonts");
        writer.WriteAttributeString("count", "2");
        WriteFont(writer, bold: false);
        WriteFont(writer, bold: true);
        writer.WriteEndElement();

        writer.WriteStartElement("fills");
        writer.WriteAttributeString("count", "3");
        writer.WriteStartElement("fill");
        writer.WriteStartElement("patternFill");
        writer.WriteAttributeString("patternType", "none");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("fill");
        writer.WriteStartElement("patternFill");
        writer.WriteAttributeString("patternType", "gray125");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("fill");
        writer.WriteStartElement("patternFill");
        writer.WriteAttributeString("patternType", "solid");
        writer.WriteStartElement("fgColor");
        writer.WriteAttributeString("rgb", "FFD9EAF7");
        writer.WriteEndElement();
        writer.WriteStartElement("bgColor");
        writer.WriteAttributeString("indexed", "64");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("borders");
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("border");
        writer.WriteElementString("left", string.Empty);
        writer.WriteElementString("right", string.Empty);
        writer.WriteElementString("top", string.Empty);
        writer.WriteElementString("bottom", string.Empty);
        writer.WriteElementString("diagonal", string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("cellStyleXfs");
        writer.WriteAttributeString("count", "1");
        WriteCellFormat(writer, "0", "0", "0", "0", applyNumberFormat: false, applyFont: false, applyFill: false);
        writer.WriteEndElement();

        writer.WriteStartElement("cellXfs");
        writer.WriteAttributeString("count", "7");
        WriteCellFormat(writer, "0", "0", "0", "0", applyNumberFormat: false, applyFont: false, applyFill: false);
        WriteCellFormat(writer, "0", "1", "2", "0", applyNumberFormat: false, applyFont: true, applyFill: true);
        WriteCellFormat(writer, "3", "0", "0", "0", applyNumberFormat: true, applyFont: false, applyFill: false);
        WriteCellFormat(writer, "4", "0", "0", "0", applyNumberFormat: true, applyFont: false, applyFill: false);
        WriteCellFormat(writer, "164", "0", "0", "0", applyNumberFormat: true, applyFont: false, applyFill: false);
        WriteCellFormat(writer, "165", "0", "0", "0", applyNumberFormat: true, applyFont: false, applyFill: false);
        WriteCellFormat(writer, "0", "1", "0", "0", applyNumberFormat: false, applyFont: true, applyFill: false);
        writer.WriteEndElement();

        writer.WriteStartElement("cellStyles");
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle");
        writer.WriteAttributeString("name", "Normal");
        writer.WriteAttributeString("xfId", "0");
        writer.WriteAttributeString("builtinId", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteWorksheet(ZipArchive archive, int sheetNumber, XlsxSheet sheet)
    {
        using var writer = CreateXmlWriter(archive.CreateEntry($"xl/worksheets/sheet{sheetNumber}.xml").Open());
        var columnCount = sheet.Headers.Count;
        var rowCount = sheet.Rows.Count + 1;
        var lastCell = $"{ColumnName(columnCount)}{Math.Max(rowCount, 1)}";

        writer.WriteStartDocument();
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("dimension");
        writer.WriteAttributeString("ref", $"A1:{lastCell}");
        writer.WriteEndElement();

        writer.WriteStartElement("sheetViews");
        writer.WriteStartElement("sheetView");
        writer.WriteAttributeString("workbookViewId", "0");
        if (sheet.FreezeHeader)
        {
            writer.WriteStartElement("pane");
            writer.WriteAttributeString("ySplit", "1");
            writer.WriteAttributeString("topLeftCell", "A2");
            writer.WriteAttributeString("activePane", "bottomLeft");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("sheetFormatPr");
        writer.WriteAttributeString("defaultRowHeight", "15");
        writer.WriteEndElement();

        WriteColumns(writer, sheet.ColumnWidths);

        writer.WriteStartElement("sheetData");
        WriteRow(writer, 1, sheet.Headers.Select(XlsxCell.Header).ToList());
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            WriteRow(writer, i + 2, sheet.Rows[i]);
        }
        writer.WriteEndElement();

        if (sheet.AutoFilter && columnCount > 0)
        {
            writer.WriteStartElement("autoFilter");
            writer.WriteAttributeString("ref", $"A1:{ColumnName(columnCount)}{Math.Max(rowCount, 1)}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static void WriteColumns(XmlWriter writer, IReadOnlyList<double> widths)
    {
        if (widths.Count == 0)
        {
            return;
        }

        writer.WriteStartElement("cols");
        for (var i = 0; i < widths.Count; i++)
        {
            writer.WriteStartElement("col");
            writer.WriteAttributeString("min", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (i + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", widths[i].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowNumber, IReadOnlyList<XlsxCell> cells)
    {
        writer.WriteStartElement("row");
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        for (var i = 0; i < cells.Count; i++)
        {
            WriteCell(writer, rowNumber, i + 1, cells[i]);
        }
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, int rowNumber, int columnNumber, XlsxCell cell)
    {
        writer.WriteStartElement("c");
        writer.WriteAttributeString("r", $"{ColumnName(columnNumber)}{rowNumber}");
        writer.WriteAttributeString("s", ((int)cell.Style).ToString(CultureInfo.InvariantCulture));

        switch (cell.Type)
        {
            case XlsxCellType.Blank:
                break;
            case XlsxCellType.Text:
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is");
                writer.WriteStartElement("t");
                writer.WriteAttributeString("xml", "space", null, "preserve");
                writer.WriteString(SanitizeXmlText(cell.TextValue ?? string.Empty));
                writer.WriteEndElement();
                writer.WriteEndElement();
                break;
            case XlsxCellType.Number:
                writer.WriteElementString("v", cell.NumberValue?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
                break;
            case XlsxCellType.Boolean:
                writer.WriteAttributeString("t", "b");
                writer.WriteElementString("v", cell.BooleanValue == true ? "1" : "0");
                break;
            case XlsxCellType.Date:
                writer.WriteElementString("v", ToExcelSerialDate(cell.DateValue!.Value).ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unsupported XLSX cell type: {cell.Type}");
        }

        writer.WriteEndElement();
    }

    private static double ToExcelSerialDate(DateTime value)
    {
        var unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return unspecified.ToOADate();
    }

    private static XmlWriter CreateXmlWriter(Stream stream)
        => XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            CloseOutput = true
        });

    private static string SanitizeXmlText(string value)
    {
        if (value.All(XmlConvert.IsXmlChar))
        {
            return value;
        }

        return new string(value.Select(ch => XmlConvert.IsXmlChar(ch) ? ch : ' ').ToArray());
    }

    private static void WriteNumFmt(XmlWriter writer, string id, string formatCode)
    {
        writer.WriteStartElement("numFmt");
        writer.WriteAttributeString("numFmtId", id);
        writer.WriteAttributeString("formatCode", formatCode);
        writer.WriteEndElement();
    }

    private static void WriteFont(XmlWriter writer, bool bold)
    {
        writer.WriteStartElement("font");
        if (bold)
        {
            writer.WriteElementString("b", string.Empty);
        }
        writer.WriteStartElement("sz");
        writer.WriteAttributeString("val", "11");
        writer.WriteEndElement();
        writer.WriteStartElement("name");
        writer.WriteAttributeString("val", "Calibri");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteCellFormat(
        XmlWriter writer,
        string numFmtId,
        string fontId,
        string fillId,
        string borderId,
        bool applyNumberFormat,
        bool applyFont,
        bool applyFill)
    {
        writer.WriteStartElement("xf");
        writer.WriteAttributeString("numFmtId", numFmtId);
        writer.WriteAttributeString("fontId", fontId);
        writer.WriteAttributeString("fillId", fillId);
        writer.WriteAttributeString("borderId", borderId);
        writer.WriteAttributeString("xfId", "0");
        if (applyNumberFormat)
        {
            writer.WriteAttributeString("applyNumberFormat", "1");
        }
        if (applyFont)
        {
            writer.WriteAttributeString("applyFont", "1");
        }
        if (applyFill)
        {
            writer.WriteAttributeString("applyFill", "1");
        }
        writer.WriteEndElement();
    }

    private static string ColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }
}

internal sealed record XlsxSheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<XlsxCell>> Rows,
    IReadOnlyList<double> ColumnWidths,
    bool FreezeHeader = true,
    bool AutoFilter = true);

internal sealed record XlsxCell(
    XlsxCellType Type,
    XlsxCellStyle Style,
    string? TextValue = null,
    decimal? NumberValue = null,
    bool? BooleanValue = null,
    DateTime? DateValue = null)
{
    public static XlsxCell Blank() => new(XlsxCellType.Blank, XlsxCellStyle.Default);
    public static XlsxCell Header(string value) => new(XlsxCellType.Text, XlsxCellStyle.Header, TextValue: value);
    public static XlsxCell Text(string? value) => string.IsNullOrEmpty(value)
        ? Blank()
        : new XlsxCell(XlsxCellType.Text, XlsxCellStyle.Default, TextValue: value);
    public static XlsxCell InfoLabel(string value) => new(XlsxCellType.Text, XlsxCellStyle.InfoLabel, TextValue: value);
    public static XlsxCell Number(decimal? value, XlsxCellStyle style) => value.HasValue
        ? new XlsxCell(XlsxCellType.Number, style, NumberValue: value.Value)
        : Blank();
    public static XlsxCell Boolean(bool value) => new(XlsxCellType.Boolean, XlsxCellStyle.Default, BooleanValue: value);
    public static XlsxCell Date(DateTime? value) => value.HasValue
        ? new XlsxCell(XlsxCellType.Date, XlsxCellStyle.Date, DateValue: value.Value)
        : Blank();
    public static XlsxCell DateTime(DateTime value) => new(XlsxCellType.Date, XlsxCellStyle.DateTime, DateValue: value);
}

internal enum XlsxCellType
{
    Blank,
    Text,
    Number,
    Boolean,
    Date
}

internal enum XlsxCellStyle
{
    Default = 0,
    Header = 1,
    Integer = 2,
    Decimal = 3,
    Date = 4,
    DateTime = 5,
    InfoLabel = 6
}
