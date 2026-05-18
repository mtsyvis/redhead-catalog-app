namespace Redhead.SitesCatalog.Api.Options;

public sealed class GoogleDriveOptions
{
    public const string SectionName = "GoogleDrive";
    public const string DriveFileScope = "https://www.googleapis.com/auth/drive.file";
    public const string DefaultExportFolderName = "Redhead Catalog Exports";

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
    public string AppName { get; set; } = "Redhead Catalog";
    public string ExportFolderName { get; set; } = DefaultExportFolderName;
}
