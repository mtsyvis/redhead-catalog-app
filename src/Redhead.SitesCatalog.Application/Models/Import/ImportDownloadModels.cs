namespace Redhead.SitesCatalog.Application.Models.Import;

public static class ImportArtifactKinds
{
    public const string InvalidRows = "invalidRows";
    public const string DuplicateInputRows = "duplicateInputRows";
}

public sealed class ImportDownloadItem
{
    public bool Available { get; set; }
    public string Token { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class ImportDownloadsInfo
{
    public ImportDownloadItem? InvalidRows { get; set; }
    public ImportDownloadItem? DuplicateInputRows { get; set; }
}

public sealed class ImportArtifactHandle
{
    public string Token { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class ImportArtifactDownload
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
    public byte[] Content { get; set; } = Array.Empty<byte>();
}

public sealed class InvalidImportRowRecord
{
    public int SourceRowNumber { get; set; }
    public List<string> RawValues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public sealed class InvalidRowsImportArtifactPayload
{
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<InvalidImportRowRecord> Rows { get; set; } = new();
}
