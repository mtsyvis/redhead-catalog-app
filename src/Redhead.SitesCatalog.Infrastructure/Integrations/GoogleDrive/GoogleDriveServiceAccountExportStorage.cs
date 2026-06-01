using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Domain.SystemExports;
using Redhead.SitesCatalog.Infrastructure.Options;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public sealed class GoogleDriveServiceAccountExportStorage : ISystemExportStorage
{
    private const string XlsxExtension = ".xlsx";

    private readonly EmergencySitesExportOptions _options;
    private readonly ILogger<GoogleDriveServiceAccountExportStorage> _logger;

    public GoogleDriveServiceAccountExportStorage(
        IOptions<EmergencySitesExportOptions> options,
        ILogger<GoogleDriveServiceAccountExportStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SystemExportUploadedFile> UploadAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var options = GetValidatedOptions();
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var driveService = CreateDriveService(options);
        var metadata = new DriveFile
        {
            Name = fileName,
            MimeType = contentType,
            Parents = [options.GoogleDriveFolderId]
        };

        var request = driveService.Files.Create(metadata, content, contentType);
        request.SupportsAllDrives = true;
        request.Fields = "id,name,size,webViewLink";

        var upload = await request.UploadAsync(cancellationToken);
        if (upload.Status != UploadStatus.Completed)
        {
            throw upload.Exception ?? new InvalidOperationException("Google Drive service-account upload failed.");
        }

        var file = request.ResponseBody
            ?? throw new InvalidOperationException("Google Drive service-account upload did not return file metadata.");

        if (string.IsNullOrWhiteSpace(file.Id))
        {
            throw new InvalidOperationException("Google Drive service-account upload did not return a file id.");
        }

        var uploadedFileName = string.IsNullOrWhiteSpace(file.Name) ? fileName : file.Name;
        var fileSize = file.Size ?? (content.CanSeek ? content.Length : 0);

        return new SystemExportUploadedFile(
            uploadedFileName,
            fileSize,
            SystemExportStorageProviders.GoogleDriveSharedDrive,
            CreateStoragePath(options.GoogleDriveFolderId!, uploadedFileName),
            file.Id,
            file.WebViewLink);
    }

    public async Task<SystemExportCleanupResult> DeleteOldFilesAsync(
        string fileNamePrefix,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNamePrefix);
        if (retention <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retention), "Retention must be greater than zero.");
        }

        var options = GetValidatedOptions();
        var cutoffUtc = DateTimeOffset.UtcNow.Subtract(retention);
        var deletedCount = 0;
        var failedCount = 0;

        using var driveService = CreateDriveService(options);
        var files = await ListCleanupCandidatesAsync(
            driveService,
            options.GoogleDriveFolderId!,
            fileNamePrefix,
            cutoffUtc,
            cancellationToken);

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.Id))
            {
                continue;
            }

            try
            {
                var delete = driveService.Files.Delete(file.Id);
                delete.SupportsAllDrives = true;
                await delete.ExecuteAsync(cancellationToken);
                deletedCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                _logger.LogError(
                    ex,
                    "Failed to delete old emergency Sites export file {FileId} ({FileName}) from Google Drive.",
                    file.Id,
                    file.Name);
            }
        }

        _logger.LogInformation(
            "Deleted {DeletedCount} old emergency Sites export files from Google Drive. Failed deletions: {FailedCount}.",
            deletedCount,
            failedCount);

        return new SystemExportCleanupResult(deletedCount, failedCount);
    }

    private static async Task<IReadOnlyList<DriveFile>> ListCleanupCandidatesAsync(
        DriveService driveService,
        string folderId,
        string fileNamePrefix,
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken)
    {
        var files = new List<DriveFile>();
        string? pageToken = null;
        var escapedFolderId = EscapeDriveQueryValue(folderId);
        var escapedPrefix = EscapeDriveQueryValue(fileNamePrefix);
        var cutoff = cutoffUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

        do
        {
            var request = driveService.Files.List();
            request.Q = $"'{escapedFolderId}' in parents and trashed = false and name contains '{escapedPrefix}' and createdTime < '{cutoff}'";
            request.Fields = "nextPageToken,files(id,name,createdTime)";
            request.PageToken = pageToken;
            request.PageSize = 1000;
            request.SupportsAllDrives = true;
            request.IncludeItemsFromAllDrives = true;
            request.Corpora = "allDrives";

            var response = await request.ExecuteAsync(cancellationToken);
            files.AddRange((response.Files ?? [])
                .Where(file =>
                    !string.IsNullOrWhiteSpace(file.Name) &&
                    file.Name.StartsWith(fileNamePrefix + "-", StringComparison.OrdinalIgnoreCase) &&
                    file.Name.EndsWith(XlsxExtension, StringComparison.OrdinalIgnoreCase) &&
                    (file.CreatedTimeDateTimeOffset ?? DateTimeOffset.MaxValue) < cutoffUtc));

            pageToken = response.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));

        return files;
    }

    private EmergencySitesExportOptions GetValidatedOptions()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Emergency Sites export storage is disabled.");
        }

        if (!EmergencySitesExportOptions.IsValid(_options))
        {
            throw new InvalidOperationException(
                "EmergencySitesExport configuration is invalid. Enabled exports require GoogleDriveFolderId, ServiceAccountJsonPath, FilePrefix, and a positive RetentionWeeks value.");
        }

        if (!File.Exists(_options.ServiceAccountJsonPath))
        {
            throw new FileNotFoundException(
                "Google service account JSON file was not found.",
                _options.ServiceAccountJsonPath);
        }

        return _options;
    }

    private static DriveService CreateDriveService(EmergencySitesExportOptions options)
    {
        var credential = CredentialFactory
            .FromFile<ServiceAccountCredential>(options.ServiceAccountJsonPath)
            .ToGoogleCredential()
            .CreateScoped(DriveService.Scope.Drive);

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Redhead Catalog"
        });
    }

    private static string CreateStoragePath(string folderId, string fileName)
        => $"google-drive://folders/{folderId}/{fileName}";

    private static string EscapeDriveQueryValue(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
}
