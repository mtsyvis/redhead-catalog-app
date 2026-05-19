using Redhead.SitesCatalog.Api.Options;

namespace Redhead.SitesCatalog.Api.Services;

public interface IGoogleDriveApiClient
{
    Task<GoogleDriveAccessToken> RefreshAccessTokenAsync(
        GoogleDriveOptions options,
        string refreshToken,
        CancellationToken cancellationToken);

    Task<GoogleDriveUser> FetchDriveUserAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<GoogleDriveFolder> EnsureExportFolderAsync(
        string accessToken,
        string? existingFolderId,
        string folderName,
        CancellationToken cancellationToken);

    Task<GoogleDriveUploadedFile> UploadFileAsync(
        string accessToken,
        string folderId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}

