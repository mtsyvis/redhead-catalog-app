namespace Redhead.SitesCatalog.Application.Exports;

public interface ISitesExcelExportGenerator
{
    MemoryStream Generate(SitesExcelExportRequest request);
}
