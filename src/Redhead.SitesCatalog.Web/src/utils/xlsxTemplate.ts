export interface XlsxTemplateOptions {
  readonly fileName: string;
  readonly sheetName: string;
  readonly rows: readonly (readonly string[])[];
}

interface ZipFileEntry {
  readonly path: string;
  readonly content: string;
}

const encoder = new TextEncoder();

const crcTable = (() => {
  const table = new Uint32Array(256);
  for (let i = 0; i < table.length; i++) {
    let value = i;
    for (let bit = 0; bit < 8; bit++) {
      value = (value & 1) !== 0 ? 0xedb88320 ^ (value >>> 1) : value >>> 1;
    }

    table[i] = value >>> 0;
  }

  return table;
})();

export function downloadXlsxTemplate({ fileName, sheetName, rows }: XlsxTemplateOptions) {
  const workbookBytes = buildXlsxWorkbook(sheetName, rows);
  const blob = new Blob([workbookBytes], {
    type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  });
  const downloadUrl = window.URL.createObjectURL(blob);
  const link = document.createElement('a');

  link.href = downloadUrl;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(downloadUrl);
}

function buildXlsxWorkbook(sheetName: string, rows: readonly (readonly string[])[]) {
  return buildZip([
    {
      path: '[Content_Types].xml',
      content:
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">' +
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
        '<Default Extension="xml" ContentType="application/xml"/>' +
        '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>' +
        '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>' +
        '</Types>',
    },
    {
      path: '_rels/.rels',
      content:
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>' +
        '</Relationships>',
    },
    {
      path: 'xl/workbook.xml',
      content:
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
        '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" ' +
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
        '<sheets>' +
        `<sheet name="${escapeXml(sheetName)}" sheetId="1" r:id="rId1"/>` +
        '</sheets>' +
        '</workbook>',
    },
    {
      path: 'xl/_rels/workbook.xml.rels',
      content:
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">' +
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>' +
        '</Relationships>',
    },
    {
      path: 'xl/worksheets/sheet1.xml',
      content: buildWorksheetXml(rows),
    },
  ]);
}

function buildWorksheetXml(rows: readonly (readonly string[])[]) {
  const maxColumns = Math.max(1, ...rows.map((row) => row.length));
  const dimension = `A1:${columnName(maxColumns)}${Math.max(1, rows.length)}`;
  const sheetRows = rows
    .map((row, rowIndex) => {
      const rowNumber = rowIndex + 1;
      const cells = row
        .map((value, columnIndex) => buildInlineStringCell(columnIndex + 1, rowNumber, value))
        .join('');

      return `<row r="${rowNumber}">${cells}</row>`;
    })
    .join('');

  return (
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>' +
    '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">' +
    `<dimension ref="${dimension}"/>` +
    '<sheetViews><sheetView workbookViewId="0"/></sheetViews>' +
    '<sheetFormatPr defaultRowHeight="15"/>' +
    buildColumnsXml(maxColumns) +
    `<sheetData>${sheetRows}</sheetData>` +
    '</worksheet>'
  );
}

function buildColumnsXml(maxColumns: number) {
  const columns = Array.from({ length: maxColumns }, (_value, index) => {
    const column = index + 1;
    return `<col min="${column}" max="${column}" width="24" customWidth="1"/>`;
  }).join('');

  return `<cols>${columns}</cols>`;
}

function buildInlineStringCell(column: number, row: number, value: string) {
  const reference = `${columnName(column)}${row}`;
  return (
    `<c r="${reference}" t="inlineStr">` +
    `<is><t xml:space="preserve">${escapeXml(value)}</t></is>` +
    '</c>'
  );
}

function buildZip(files: readonly ZipFileEntry[]) {
  const chunks: Uint8Array[] = [];
  const centralDirectoryChunks: Uint8Array[] = [];
  let offset = 0;

  for (const file of files) {
    const fileNameBytes = encoder.encode(file.path);
    const contentBytes = encoder.encode(file.content);
    const crc = crc32(contentBytes);
    const localHeader = new BinaryWriter(30 + fileNameBytes.length);

    localHeader.uint32(0x04034b50);
    localHeader.uint16(20);
    localHeader.uint16(0);
    localHeader.uint16(0);
    localHeader.uint16(0);
    localHeader.uint16(0);
    localHeader.uint32(crc);
    localHeader.uint32(contentBytes.length);
    localHeader.uint32(contentBytes.length);
    localHeader.uint16(fileNameBytes.length);
    localHeader.uint16(0);
    localHeader.bytes(fileNameBytes);

    chunks.push(localHeader.buffer, contentBytes);

    const centralHeader = new BinaryWriter(46 + fileNameBytes.length);
    centralHeader.uint32(0x02014b50);
    centralHeader.uint16(20);
    centralHeader.uint16(20);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint32(crc);
    centralHeader.uint32(contentBytes.length);
    centralHeader.uint32(contentBytes.length);
    centralHeader.uint16(fileNameBytes.length);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint16(0);
    centralHeader.uint32(0);
    centralHeader.uint32(offset);
    centralHeader.bytes(fileNameBytes);

    centralDirectoryChunks.push(centralHeader.buffer);
    offset += localHeader.buffer.length + contentBytes.length;
  }

  const centralDirectoryOffset = offset;
  const centralDirectorySize = centralDirectoryChunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const endOfCentralDirectory = new BinaryWriter(22);

  endOfCentralDirectory.uint32(0x06054b50);
  endOfCentralDirectory.uint16(0);
  endOfCentralDirectory.uint16(0);
  endOfCentralDirectory.uint16(files.length);
  endOfCentralDirectory.uint16(files.length);
  endOfCentralDirectory.uint32(centralDirectorySize);
  endOfCentralDirectory.uint32(centralDirectoryOffset);
  endOfCentralDirectory.uint16(0);

  return concatBytes([...chunks, ...centralDirectoryChunks, endOfCentralDirectory.buffer]);
}

function crc32(bytes: Uint8Array) {
  let crc = 0xffffffff;
  for (const byte of bytes) {
    crc = crcTable[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  }

  return (crc ^ 0xffffffff) >>> 0;
}

function concatBytes(chunks: readonly Uint8Array[]) {
  const length = chunks.reduce((sum, chunk) => sum + chunk.length, 0);
  const result = new Uint8Array(length);
  let offset = 0;

  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.length;
  }

  return result;
}

function columnName(columnNumber: number) {
  let column = columnNumber;
  let name = '';

  while (column > 0) {
    const remainder = (column - 1) % 26;
    name = String.fromCharCode(65 + remainder) + name;
    column = Math.floor((column - 1) / 26);
  }

  return name;
}

function escapeXml(value: string) {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

class BinaryWriter {
  public readonly buffer: Uint8Array;

  private readonly view: DataView;
  private offset = 0;

  public constructor(length: number) {
    this.buffer = new Uint8Array(length);
    this.view = new DataView(this.buffer.buffer);
  }

  public uint16(value: number) {
    this.view.setUint16(this.offset, value, true);
    this.offset += 2;
  }

  public uint32(value: number) {
    this.view.setUint32(this.offset, value, true);
    this.offset += 4;
  }

  public bytes(value: Uint8Array) {
    this.buffer.set(value, this.offset);
    this.offset += value.length;
  }
}
