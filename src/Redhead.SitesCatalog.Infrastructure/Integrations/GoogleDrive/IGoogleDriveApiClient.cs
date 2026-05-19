using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public interface IGoogleDriveApiClient
{
    Task<GoogleDriveTokenSet> ExchangeAuthorizationCodeAsync(
        GoogleDriveOptions options,
        string code,
        CancellationToken cancellationToken);

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
