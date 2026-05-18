using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Options;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveIntegrationService : IGoogleDriveIntegrationService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string DriveAboutEndpoint = "https://www.googleapis.com/drive/v3/about?fields=user(emailAddress)";
    private const string DriveFilesEndpoint = "https://www.googleapis.com/drive/v3/files";
    private const string FolderMimeType = "application/vnd.google-apps.folder";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGoogleDriveOAuthStateService _stateService;
    private readonly IGoogleDriveTokenProtector _tokenProtector;
    private readonly GoogleDriveOptions _googleDriveOptions;

    public GoogleDriveIntegrationService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IGoogleDriveOAuthStateService stateService,
        IGoogleDriveTokenProtector tokenProtector,
        IOptions<GoogleDriveOptions> googleDriveOptions)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _stateService = stateService;
        _tokenProtector = tokenProtector;
        _googleDriveOptions = googleDriveOptions.Value;
    }

    public async Task<GoogleDriveStatusResponse> GetStatusAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var activeConnection = await _context.GoogleDriveConnections
            .AsNoTracking()
            .Where(connection => connection.UserId == userId && connection.RevokedAtUtc == null)
            .OrderByDescending(connection => connection.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeConnection != null)
        {
            return new GoogleDriveStatusResponse(
                true,
                activeConnection.GoogleEmail,
                activeConnection.ConnectedAtUtc,
                activeConnection.ExportFolderName,
                !string.IsNullOrWhiteSpace(activeConnection.ExportFolderId),
                false,
                false);
        }

        var latestConnection = await _context.GoogleDriveConnections
            .AsNoTracking()
            .Where(connection => connection.UserId == userId)
            .OrderByDescending(connection => connection.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return new GoogleDriveStatusResponse(
            false,
            null,
            null,
            null,
            false,
            latestConnection?.RevokedAtUtc != null,
            latestConnection?.RevokedAtUtc != null);
    }

    public string CreateAuthorizationUrl(string userId)
    {
        var options = GetValidatedGoogleOptions();
        var state = _stateService.CreateState(userId);

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = GoogleDriveOptions.DriveFileScope,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        return QueryHelpers.AddQueryString(AuthorizationEndpoint, parameters);
    }

    public async Task CompleteConnectionAsync(
        string userId,
        string? code,
        string? state,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedGoogleOptions();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GoogleDriveIntegrationException("Google authorization code was not returned.");
        }

        if (!_stateService.ValidateAndConsumeState(userId, state))
        {
            throw new GoogleDriveIntegrationException("Google authorization state is invalid or expired.");
        }

        var activeConnection = await _context.GoogleDriveConnections
            .Where(connection => connection.UserId == userId && connection.RevokedAtUtc == null)
            .OrderByDescending(connection => connection.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var tokenResponse = await ExchangeCodeForTokensAsync(options, code, cancellationToken);
        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new GoogleDriveIntegrationException("Google token response did not include an access token.");
        }

        var refreshTokenEncrypted = activeConnection?.RefreshTokenEncrypted;
        if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
        {
            refreshTokenEncrypted = _tokenProtector.ProtectRefreshToken(tokenResponse.RefreshToken);
        }

        if (string.IsNullOrWhiteSpace(refreshTokenEncrypted))
        {
            throw new GoogleDriveIntegrationException("Google token response did not include a refresh token.");
        }

        var driveUser = await FetchDriveUserAsync(tokenResponse.AccessToken, cancellationToken);
        var now = DateTime.UtcNow;
        var connection = activeConnection ?? new GoogleDriveConnection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConnectedAtUtc = now
        };

        connection.GoogleEmail = driveUser.EmailAddress;
        connection.RefreshTokenEncrypted = refreshTokenEncrypted;
        connection.GrantedScopes = NormalizeGrantedScopes(tokenResponse.Scope);
        connection.UpdatedAtUtc = now;
        connection.RevokedAtUtc = null;
        connection.LastError = null;

        var folder = await EnsureExportFolderAsync(
            tokenResponse.AccessToken,
            connection.ExportFolderId,
            GetExportFolderName(options),
            cancellationToken);

        connection.ExportFolderId = folder.Id;
        connection.ExportFolderName = folder.Name;

        if (activeConnection == null)
        {
            _context.GoogleDriveConnections.Add(connection);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DisconnectAsync(string userId, CancellationToken cancellationToken)
    {
        var connection = await _context.GoogleDriveConnections
            .Where(item => item.UserId == userId && item.RevokedAtUtc == null)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (connection == null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        connection.RevokedAtUtc = now;
        connection.UpdatedAtUtc = now;

        // Local disconnect intentionally does not call Google's revoke endpoint yet.
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<GoogleTokenResponse> ExchangeCodeForTokensAsync(
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
            throw new GoogleDriveIntegrationException("Google token exchange failed.");
        }

        return await DeserializeJsonResponseAsync<GoogleTokenResponse>(response, cancellationToken)
            ?? throw new GoogleDriveIntegrationException("Google token response could not be read.");
    }

    private async Task<GoogleDriveUser> FetchDriveUserAsync(
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

    private async Task<GoogleDriveFolder> EnsureExportFolderAsync(
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

    private async Task<GoogleDriveFolder?> TryGetExistingFolderAsync(
        string accessToken,
        string folderId,
        CancellationToken cancellationToken)
    {
        var url = $"{DriveFilesEndpoint}/{Uri.EscapeDataString(folderId)}?fields=id,name,mimeType,trashed";
        using var request = CreateAuthorizedGetRequest(url, accessToken);
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
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
            throw new GoogleDriveIntegrationException("Google Drive export folder could not be created.");
        }

        var folder = await DeserializeJsonResponseAsync<GoogleDriveFileResponse>(response, cancellationToken)
            ?? throw new GoogleDriveIntegrationException("Google Drive folder response could not be read.");

        if (string.IsNullOrWhiteSpace(folder.Id))
        {
            throw new GoogleDriveIntegrationException("Google Drive folder response did not include a folder id.");
        }

        return new GoogleDriveFolder(folder.Id, string.IsNullOrWhiteSpace(folder.Name) ? folderName : folder.Name);
    }

    private GoogleDriveOptions GetValidatedGoogleOptions()
    {
        if (string.IsNullOrWhiteSpace(_googleDriveOptions.ClientId) ||
            string.IsNullOrWhiteSpace(_googleDriveOptions.ClientSecret) ||
            string.IsNullOrWhiteSpace(_googleDriveOptions.RedirectUri))
        {
            throw new GoogleDriveConfigurationException(
                "GoogleDrive:ClientId, GoogleDrive:ClientSecret, and GoogleDrive:RedirectUri are required.");
        }

        return _googleDriveOptions;
    }

    private static string GetExportFolderName(GoogleDriveOptions options)
        => string.IsNullOrWhiteSpace(options.ExportFolderName)
            ? GoogleDriveOptions.DefaultExportFolderName
            : options.ExportFolderName.Trim();

    private static string NormalizeGrantedScopes(string? scopes)
        => string.Join(
            ' ',
            (scopes ?? GoogleDriveOptions.DriveFileScope)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

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

    private sealed record GoogleDriveUser(string? EmailAddress);

    private sealed record GoogleDriveFolder(string Id, string? Name);

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
        [property: JsonPropertyName("trashed")] bool Trashed);

    private sealed record GoogleDriveCreateFolderRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("mimeType")] string MimeType);
}
