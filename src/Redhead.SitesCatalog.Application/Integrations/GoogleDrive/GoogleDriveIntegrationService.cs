using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Application.Exceptions;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Integrations.GoogleDrive;
using Redhead.SitesCatalog.Infrastructure.Options;

namespace Redhead.SitesCatalog.Application.Integrations.GoogleDrive;

public sealed class GoogleDriveIntegrationService : IGoogleDriveIntegrationService
{
    private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";

    private readonly ApplicationDbContext _context;
    private readonly IGoogleDriveApiClient _googleDriveApiClient;
    private readonly IGoogleDriveOAuthStateService _stateService;
    private readonly IGoogleDriveTokenProtector _tokenProtector;
    private readonly GoogleDriveOptions _googleDriveOptions;

    public GoogleDriveIntegrationService(
        ApplicationDbContext context,
        IGoogleDriveApiClient googleDriveApiClient,
        IGoogleDriveOAuthStateService stateService,
        IGoogleDriveTokenProtector tokenProtector,
        IOptions<GoogleDriveOptions> googleDriveOptions)
    {
        _context = context;
        _googleDriveApiClient = googleDriveApiClient;
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

        return AddQueryString(AuthorizationEndpoint, parameters);
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

        var tokenResponse = await _googleDriveApiClient.ExchangeAuthorizationCodeAsync(
            options,
            code,
            cancellationToken);
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

        var driveUser = await _googleDriveApiClient.FetchDriveUserAsync(tokenResponse.AccessToken, cancellationToken);
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

        var folder = await _googleDriveApiClient.EnsureExportFolderAsync(
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

    private static string AddQueryString(string uri, IReadOnlyDictionary<string, string?> parameters)
    {
        var query = string.Join(
            '&',
            parameters
                .Where(parameter => parameter.Value != null)
                .Select(parameter =>
                    $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value!)}"));

        return $"{uri}?{query}";
    }
}
