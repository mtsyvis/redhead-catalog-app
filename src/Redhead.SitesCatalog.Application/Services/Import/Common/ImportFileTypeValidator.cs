using Redhead.SitesCatalog.Application.Services.Import.Csv;

namespace Redhead.SitesCatalog.Application.Services.Import.Common;

public static class ImportFileTypeValidator
{
    public static bool IsCsvFile(string fileName, string? contentType)
        => CsvImportHelper.IsCsvExtension(fileName) || CsvImportHelper.IsCsvContentType(contentType);
}
