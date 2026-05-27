namespace Redhead.SitesCatalog.Application.Models.Import;

public static class ImportArtifactKinds
{
    public const string InvalidRows = "invalidRows";
    public const string DuplicateInputRows = "duplicateInputRows";
    public const string UnmatchedRows = "unmatchedRows";
    public const string WarningRows = "warningRows";
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
    public ImportDownloadItem? UnmatchedRows { get; set; }
    public ImportDownloadItem? WarningRows { get; set; }
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

public sealed class UnmatchedImportRowRecord
{
    public int SourceRowNumber { get; set; }
    public List<string> RawValues { get; set; } = new();
}

public sealed class UnmatchedRowsImportArtifactPayload
{
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<UnmatchedImportRowRecord> Rows { get; set; } = new();
}

public sealed class WarningImportRowRecord
{
    public string Domain { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int SourceRowNumber { get; set; }
    public string WarningDetails { get; set; } = string.Empty;
}

public sealed class WarningRowsImportArtifactPayload
{
    public List<WarningImportRowRecord> Rows { get; set; } = new();
}
