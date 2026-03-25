using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

public interface IImportArtifactStorageService
{
    ImportArtifactHandle StoreInvalidRows(string importType, InvalidRowsImportArtifactPayload payload);
    ImportArtifactDownload? GetCsvDownload(string token);
}
