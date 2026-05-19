using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;

public sealed class GoogleDriveApiClient : IGoogleDriveApiClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveAboutEndpoint = "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)";
    private const string DriveFilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string DriveUploadEndpoint = "https://www.googleapis.com/upload/drive/v3/files";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleDriveApiClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<GoogleDriveTokenSet> ExchangeAuthorizationCodeAsync(
        GoogleDriveOptions options,
        string code,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["code"] = code,
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["redirect_uri"] = options.RedirectUri,
                ["grant_type"] = "authorization_code"
            }!)
        };

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response,
                "Google token exchange failed.",
                cancellationToken);
        }

        var token = await DeserializeJsonResponseAsync<GoogleTokenResponse>(response, cancellationToken)
            ?? throw new GoogleDriveApiException("Google token response could not be read.");

        return new GoogleDriveTokenSet(token.AccessToken, token.RefreshToken, token.Scope);
    }

    public async Task<GoogleDriveAccessToken> RefreshAccessTokenAsync(
        GoogleDriveOptions options,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }!)
        };

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response,
                "Google Drive access token could not be refreshed.",
                cancellationToken);
        }

        var token = await DeserializeJsonResponseAsync<GoogleTokenResponse>(response, cancellationToken)
            ?? throw new GoogleDriveApiException("Google token response could not be read.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new GoogleDriveApiException("Google token response did not include an access token.");
        }

        return new GoogleDriveAccessToken(token.AccessToken);
    }

    public async Task<GoogleDriveUser> FetchDriveUserAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedGetRequest(DriveAboutEndpoint, accessToken);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new GoogleDriveUser(null);
        }

        var about = await DeserializeJsonResponseAsync<GoogleDriveAboutResponse>(response, cancellationToken);
        return new GoogleDriveUser(about?.User?.EmailAddress);
    }

    public async Task<GoogleDriveFolder> EnsureExportFolderAsync(
        string accessToken,
        string? existingFolderId,
        string folderName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(existingFolderId))
        {
            var existingFolder = await TryGetExistingFolderAsync(accessToken, existingFolderId, cancellationToken);
            if (existingFolder != null)
            {
                return existingFolder;
            }
        }

        return await CreateFolderAsync(accessToken, folderName, cancellationToken);
    }

    public async Task<GoogleDriveUploadedFile> UploadFileAsync(
        string accessToken,
        string folderId,
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{DriveUploadEndpoint}?uploadType=multipart&fields=id,name,webViewLink");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var metadata = new GoogleDriveUploadMetadata(
            fileName,
            contentType,
            [folderId]);

        using var multipart = new MultipartContent("related");
        multipart.Add(new StringContent(
            JsonSerializer.Serialize(metadata, JsonOptions),
            Encoding.UTF8,
            "application/json"));

        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(fileContent);
        request.Content = multipart;

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response,
                "Google Drive file upload failed.",
                cancellationToken);
        }

        var file = await DeserializeJsonResponseAsync<GoogleDriveFileResponse>(response, cancellationToken)
            ?? throw new GoogleDriveApiException("Google Drive upload response could not be read.");

        if (string.IsNullOrWhiteSpace(file.Id))
        {
            throw new GoogleDriveApiException("Google Drive upload response did not include a file id.");
        }

        return new GoogleDriveUploadedFile(
            file.Id,
            string.IsNullOrWhiteSpace(file.Name) ? fileName : file.Name,
            file.WebViewLink);
    }

    private async Task<GoogleDriveFolder?> TryGetExistingFolderAsync(
        string accessToken,
        string folderId,
        CancellationToken cancellationToken)
    {
        var url = $"{DriveFilesEndpoint}/{Uri.EscapeDataString(folderId)}?fields=id,name,mimeType,trashed";
        using var request = CreateAuthorizedGetRequest(url, accessToken);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response,
                "Google Drive export folder could not be verified.",
                cancellationToken);
        }

        var file = await DeserializeJsonResponseAsync<GoogleDriveFileResponse>(response, cancellationToken);
        if (file == null ||
            file.Trashed ||
            !string.Equals(file.MimeType, FolderMimeType, StringComparison.Ordinal))
        {
            return null;
        }

        return new GoogleDriveFolder(file.Id, file.Name);
    }

    private async Task<GoogleDriveFolder> CreateFolderAsync(
        string accessToken,
        string folderName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{DriveFilesEndpoint}?fields=id,name");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new GoogleDriveCreateFolderRequest(folderName, FolderMimeType), JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(
                response,
                "Google Drive export folder could not be created.",
                cancellationToken);
        }

        var folder = await DeserializeJsonResponseAsync<GoogleDriveFileResponse>(response, cancellationToken)
            ?? throw new GoogleDriveApiException("Google Drive folder response could not be read.");

        if (string.IsNullOrWhiteSpace(folder.Id))
        {
            throw new GoogleDriveApiException("Google Drive folder response did not include a folder id.");
        }

        return new GoogleDriveFolder(folder.Id, string.IsNullOrWhiteSpace(folder.Name) ? folderName : folder.Name);
    }

    private static HttpRequestMessage CreateAuthorizedGetRequest(string url, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task<T?> DeserializeJsonResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task<GoogleDriveApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        string message,
        CancellationToken cancellationToken)
    {
        var error = await TryDeserializeJsonResponseAsync<GoogleErrorResponse>(response, cancellationToken);
        var reconnectRequired =
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
            string.Equals(error?.Error, "invalid_grant", StringComparison.OrdinalIgnoreCase);

        return new GoogleDriveApiException(message, reconnectRequired);
    }

    private static async Task<T?> TryDeserializeJsonResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            return await DeserializeJsonResponseAsync<T>(response, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record GoogleDriveAboutResponse(
        [property: JsonPropertyName("user")] GoogleDriveAboutUser? User);

    private sealed record GoogleDriveAboutUser(
        [property: JsonPropertyName("emailAddress")] string? EmailAddress);

    private sealed record GoogleDriveFileResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("mimeType")] string? MimeType,
        [property: JsonPropertyName("trashed")] bool Trashed,
        [property: JsonPropertyName("webViewLink")] string? WebViewLink);

    private sealed record GoogleDriveCreateFolderRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("mimeType")] string MimeType);

    private sealed record GoogleDriveUploadMetadata(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("parents")] IReadOnlyList<string> Parents);

    private sealed record GoogleErrorResponse(
        [property: JsonPropertyName("error")] string? Error);
}
