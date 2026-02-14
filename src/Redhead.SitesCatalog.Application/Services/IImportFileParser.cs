using Redhead.SitesCatalog.Application.Models.Import;

namespace Redhead.SitesCatalog.Application.Services;

/// <summary>
/// Parses import files (CSV) into rows. Isolates format-specific logic.
/// </summary>
public interface IImportFileParser
{
    /// <summary>
    /// Whether this parser handles the given file (by extension or content type)
    /// </summary>
    bool CanParse(string fileName, string? contentType);

    /// <summary>
    /// Read rows from the stream. First row may be header; row numbers are 1-based.
    /// </summary>
    IAsyncEnumerable<SitesImportRowDto> ReadRowsAsync(Stream stream, CancellationToken cancellationToken = default);
}
