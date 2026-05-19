using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Options;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveExportService : IGoogleDriveExportService
{
    private readonly ApplicationDbContext _context;
    private readonly IExportService _exportService;
    private readonly IGoogleDriveApiClient _googleDriveApiClient;
    private readonly IGoogleDriveTokenProtector _tokenProtector;
    private readonly GoogleDriveOptions _googleDriveOptions;

    public GoogleDriveExportService(
        ApplicationDbContext context,
        IExportService exportService,
        IGoogleDriveApiClient googleDriveApiClient,
        IGoogleDriveTokenProtector tokenProtector,
        IOptions<GoogleDriveOptions> googleDriveOptions)
    {
        _context = context;
        _exportService = exportService;
        _googleDriveApiClient = googleDriveApiClient;
        _tokenProtector = tokenProtector;
        _googleDriveOptions = googleDriveOptions.Value;
    }

    public async Task<GoogleDriveExportResponse> ExportSitesAsync(
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken)
    {
        var connection = await GetActiveConnectionAsync(userId, cancellationToken);
        if (connection == null)
        {
            throw GoogleDriveExportException.NotConnected();
        }

        var export = await _exportService.ExportSitesAsExcelAsync(
            query,
            userId,
            userEmail,
            userRole,
            cancellationToken);

        return await UploadExportAsync(connection, export, cancellationToken);
    }

    public async Task<GoogleDriveExportResponse> ExportMultiSearchAsync(
        string searchText,
        SitesQuery query,
        string userId,
        string userEmail,
        string userRole,
        CancellationToken cancellationToken)
    {
        var connection = await GetActiveConnectionAsync(userId, cancellationToken);
        if (connection == null)
        {
            throw GoogleDriveExportException.NotConnected();
        }

        var export = await _exportService.ExportMultiSearchAsExcelAsync(
            searchText,
            query,
            userId,
            userEmail,
            userRole,
            cancellationToken);

        return await UploadExportAsync(connection, export, cancellationToken);
    }

    private async Task<GoogleDriveExportResponse> UploadExportAsync(
        GoogleDriveConnection connection,
        ExportResult export,
        CancellationToken cancellationToken)
    {
        var options = GetValidatedOptions();
        var folderName = GetExportFolderName(options);
        var exportedAtUtc = DateTime.UtcNow;

        try
        {
            var refreshToken = _tokenProtector.UnprotectRefreshToken(connection.RefreshTokenEncrypted);
            var accessToken = await _googleDriveApiClient.RefreshAccessTokenAsync(
                options,
                refreshToken,
                cancellationToken);

            var folder = await _googleDriveApiClient.EnsureExportFolderAsync(
                accessToken.AccessToken,
                connection.ExportFolderId,
                folderName,
                cancellationToken);

            connection.ExportFolderId = folder.Id;
            connection.ExportFolderName = string.IsNullOrWhiteSpace(folder.Name) ? folderName : folder.Name;
            connection.LastError = null;
            connection.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var uploadedFile = await _googleDriveApiClient.UploadFileAsync(
                accessToken.AccessToken,
                folder.Id,
                ExportConstants.SitesFileName,
                export.FileStream,
                ExportConstants.ExcelContentType,
                cancellationToken);

            return new GoogleDriveExportResponse(
                uploadedFile.Id,
                uploadedFile.Name,
                uploadedFile.WebViewLink,
                export.ExportedRows,
                export.Truncated,
                exportedAtUtc,
                $"Google Drive / {connection.ExportFolderName}");
        }
        catch (GoogleDriveApiException ex) when (ex.ReconnectRequired)
        {
            await MarkReconnectRequiredAsync(connection, cancellationToken);
            throw GoogleDriveExportException.ReconnectRequired();
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or ArgumentException)
        {
            await MarkReconnectRequiredAsync(connection, cancellationToken);
            throw GoogleDriveExportException.ReconnectRequired();
        }
        catch (GoogleDriveApiException)
        {
            throw GoogleDriveExportException.UploadFailed();
        }
    }

    private async Task<GoogleDriveConnection?> GetActiveConnectionAsync(
        string userId,
        CancellationToken cancellationToken)
        => await _context.GoogleDriveConnections
            .Where(connection => connection.UserId == userId && connection.RevokedAtUtc == null)
            .OrderByDescending(connection => connection.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task MarkReconnectRequiredAsync(
        GoogleDriveConnection connection,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        connection.RevokedAtUtc = now;
        connection.UpdatedAtUtc = now;
        connection.LastError = "GoogleDriveReconnectRequired";

        await _context.SaveChangesAsync(cancellationToken);
    }

    private GoogleDriveOptions GetValidatedOptions()
    {
        if (string.IsNullOrWhiteSpace(_googleDriveOptions.ClientId) ||
            string.IsNullOrWhiteSpace(_googleDriveOptions.ClientSecret))
        {
            throw GoogleDriveExportException.ConfigurationMissing();
        }

        return _googleDriveOptions;
    }

    private static string GetExportFolderName(GoogleDriveOptions options)
        => string.IsNullOrWhiteSpace(options.ExportFolderName)
            ? GoogleDriveOptions.DefaultExportFolderName
            : options.ExportFolderName.Trim();
}
