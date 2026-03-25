using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppPolicies.AdminAccess)]
public class ImportController : ControllerBase
{
    private readonly ISitesImportService _sitesImportService;
    private readonly IQuarantineImportService _quarantineImportService;
    private readonly ILastPublishedImportService _lastPublishedImportService;
    private readonly ISitesUpdateImportService _sitesUpdateImportService;
    private readonly IImportArtifactStorageService _importArtifactStorageService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        ISitesImportService sitesImportService,
        IQuarantineImportService quarantineImportService,
        ILastPublishedImportService lastPublishedImportService,
        ISitesUpdateImportService sitesUpdateImportService,
        IImportArtifactStorageService importArtifactStorageService,
        ILogger<ImportController> logger)
    {
        _sitesImportService = sitesImportService;
        _quarantineImportService = quarantineImportService;
        _lastPublishedImportService = lastPublishedImportService;
        _sitesUpdateImportService = sitesUpdateImportService;
        _importArtifactStorageService = importArtifactStorageService;
        _logger = logger;
    }

    [HttpGet("/api/imports/downloads/{token}")]
    public ActionResult DownloadImportArtifact(string token)
    {
        var download = _importArtifactStorageService.GetCsvDownload(token);
        if (download is null)
        {
            return NotFound(new ApiErrorResponse("Import download artifact was not found or has expired.", StatusCodes.Status404NotFound));
        }

        return File(download.Content, download.ContentType, download.FileName);
    }

    /// <summary>
    /// Import sites from CSV (add-only). Duplicates are skipped; errors are reported.
    /// </summary>
    [HttpPost("sites")]
    [RequestSizeLimit((int)ImportConstants.MaxSitesImportFileSizeBytes)]
    public async Task<ActionResult<SitesImportResult>> ImportSites(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Sites import: no file or empty file");
            return BadRequest(new ApiErrorResponse("No file or empty file.", StatusCodes.Status400BadRequest));
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning("Sites import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new ApiErrorResponse(ImportConstants.FileTooLargeMessage, StatusCodes.Status413PayloadTooLarge));
        }

        if (!TryGetUserContext("Sites import", out var userId, out var userEmail, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var (stream, fileReadError) = await ReadFileToMemoryStreamAsync("Sites import", file, cancellationToken);
        if (fileReadError != null)
        {
            stream.Dispose();
            return fileReadError;
        }

        await using (stream)
        {
            var result = await _sitesImportService.ImportAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId,
                userEmail,
                cancellationToken);

            _logger.LogInformation(
                "Sites import succeeded. FileName={FileName}, Inserted={Inserted}, Duplicates={Duplicates}, Errors={Errors}",
                file.FileName, result.Inserted, result.DuplicatesCount, result.ErrorsCount);

            return Ok(result);
        }
    }

    /// <summary>
    /// Import quarantine from CSV (Domain, Reason). Updates existing sites by exact normalized domain match.
    /// </summary>
    [HttpPost("quarantine")]
    [RequestSizeLimit((int)ImportConstants.MaxSitesImportFileSizeBytes)]
    public async Task<ActionResult<SitesUpdateImportResult>> ImportQuarantine(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Quarantine import: no file or empty file");
            return BadRequest(new ApiErrorResponse("No file or empty file.", StatusCodes.Status400BadRequest));
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning(
                "Quarantine import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new ApiErrorResponse(ImportConstants.FileTooLargeMessage, StatusCodes.Status413PayloadTooLarge));
        }

        if (!TryGetUserContext("Quarantine import", out var userId, out var userEmail, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var (stream, fileReadError) = await ReadFileToMemoryStreamAsync("Quarantine import", file, cancellationToken);
        if (fileReadError != null)
        {
            stream.Dispose();
            return fileReadError;
        }

        await using (stream)
        {
            var result = await _quarantineImportService.ImportAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId,
                userEmail,
                cancellationToken);

            _logger.LogInformation(
                "Quarantine import succeeded. FileName={FileName}, Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}",
                file.FileName, result.Matched, result.Unmatched.Count, result.ErrorsCount);

            return Ok(result);
        }
    }

    /// <summary>
    /// Import Last Published Date from CSV (Domain, LastPublishedDate). Updates existing sites by exact normalized domain match.
    /// </summary>
    [HttpPost("last-published")]
    [RequestSizeLimit((int)ImportConstants.MaxSitesImportFileSizeBytes)]
    public async Task<ActionResult<SitesUpdateImportResult>> ImportLastPublished(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Last Published import: no file or empty file");
            return BadRequest(new ApiErrorResponse("No file or empty file.", StatusCodes.Status400BadRequest));
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning(
                "Last Published import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new ApiErrorResponse(ImportConstants.FileTooLargeMessage, StatusCodes.Status413PayloadTooLarge));
        }

        if (!TryGetUserContext("Last Published import", out var userId, out var userEmail, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var (stream, fileReadError) = await ReadFileToMemoryStreamAsync("Last Published import", file, cancellationToken);
        if (fileReadError != null)
        {
            stream.Dispose();
            return fileReadError;
        }

        await using (stream)
        {
            var result = await _lastPublishedImportService.ImportAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId,
                userEmail,
                cancellationToken);

            _logger.LogInformation(
                "Last Published import succeeded. FileName={FileName}, Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}",
                file.FileName, result.Matched, result.Unmatched.Count, result.ErrorsCount);

            return Ok(result);
        }
    }

    /// <summary>
    /// Mass-update existing sites from CSV (same columns as sites import). Domain is lookup key only.
    /// </summary>
    [HttpPost("sites-update")]
    [RequestSizeLimit((int)ImportConstants.MaxSitesImportFileSizeBytes)]
    public async Task<ActionResult<SitesUpdateImportResult>> ImportSitesUpdate(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Sites update import: no file or empty file");
            return BadRequest(new ApiErrorResponse("No file or empty file.", StatusCodes.Status400BadRequest));
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning(
                "Sites update import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                new ApiErrorResponse(ImportConstants.FileTooLargeMessage, StatusCodes.Status413PayloadTooLarge));
        }

        if (!TryGetUserContext("Sites update import", out var userId, out var userEmail, out var unauthorizedResult))
        {
            return unauthorizedResult;
        }

        var (stream, fileReadError) = await ReadFileToMemoryStreamAsync("Sites update import", file, cancellationToken);
        if (fileReadError != null)
        {
            stream.Dispose();
            return fileReadError;
        }

        await using (stream)
        {
            var result = await _sitesUpdateImportService.ImportAsync(
                stream,
                file.FileName,
                file.ContentType,
                userId,
                userEmail,
                cancellationToken);

            _logger.LogInformation(
                "Sites update import succeeded. FileName={FileName}, Matched={Matched}, Unmatched={Unmatched}, Errors={Errors}, Duplicates={Duplicates}",
                file.FileName, result.Matched, result.Unmatched.Count, result.ErrorsCount, result.DuplicatesCount);

            return Ok(result);
        }
    }

    // We standardize error responses to a consistent { error = "..." } JSON shape
    // so that clients can handle failures in a uniform and predictable way.
    private bool TryGetUserContext(
        string operationName,
        out string userId,
        out string userEmail,
        out ActionResult unauthorizedResult)
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        userEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userEmail))
        {
            unauthorizedResult = default!;
            return true;
        }

        _logger.LogWarning("{Operation}: user claims missing (UserId or Email)", operationName);
        unauthorizedResult = Unauthorized(new ApiErrorResponse("User information not found.", StatusCodes.Status401Unauthorized));
        return false;
    }

    private async Task<(MemoryStream Stream, ActionResult? ErrorResult)> ReadFileToMemoryStreamAsync(
        string operationName,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();

        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        if (stream.Length == 0)
        {
            _logger.LogWarning(
                "{Operation}: file stream is empty after read. FileName={FileName}, DeclaredLength={Length}",
                operationName,
                file.FileName,
                file.Length);

            return (stream, BadRequest(new ApiErrorResponse("The file could not be read. Ensure the file is a valid CSV and try again.", StatusCodes.Status400BadRequest)));
        }

        return (stream, null);
    }
}
