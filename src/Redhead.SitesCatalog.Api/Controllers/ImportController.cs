using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        ISitesImportService sitesImportService,
        IQuarantineImportService quarantineImportService,
        ILogger<ImportController> logger)
    {
        _sitesImportService = sitesImportService;
        _quarantineImportService = quarantineImportService;
        _logger = logger;
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
            return BadRequest(new { error = "No file or empty file." });
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning("Sites import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(413, new { error = ImportConstants.FileTooLargeMessage });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Sites import: user claims missing (UserId or Email)");
            return Unauthorized("User information not found");
        }

        await using var stream = new MemoryStream((int)file.Length);
        await using (var source = file.OpenReadStream())
        {
            if (source.CanSeek && source.Position != 0)
            {
                source.Seek(0, SeekOrigin.Begin);
            }
            await source.CopyToAsync(stream, cancellationToken);
        }
        stream.Position = 0;
        if (stream.Length == 0)
        {
            _logger.LogWarning("Sites import: file stream is empty after read. FileName={FileName}, DeclaredLength={Length}", file.FileName, file.Length);
            return BadRequest(new { error = "The file could not be read. Ensure the file is a valid CSV and try again." });
        }
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

    /// <summary>
    /// Import quarantine from CSV (Domain, Reason). Updates existing sites by exact normalized domain match.
    /// </summary>
    [HttpPost("quarantine")]
    [RequestSizeLimit((int)ImportConstants.MaxSitesImportFileSizeBytes)]
    public async Task<ActionResult<QuarantineImportResult>> ImportQuarantine(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Quarantine import: no file or empty file");
            return BadRequest(new { error = "No file or empty file." });
        }

        if (file.Length > ImportConstants.MaxSitesImportFileSizeBytes)
        {
            _logger.LogWarning(
                "Quarantine import: file too large. FileName={FileName}, Length={Length}, MaxBytes={MaxBytes}",
                file.FileName, file.Length, ImportConstants.MaxSitesImportFileSizeBytes);
            return StatusCode(413, new { error = ImportConstants.FileTooLargeMessage });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userEmail))
        {
            _logger.LogWarning("Quarantine import: user claims missing (UserId or Email)");
            return Unauthorized("User information not found");
        }

        await using var stream = new MemoryStream((int)file.Length);
        await using (var source = file.OpenReadStream())
        {
            if (source.CanSeek && source.Position != 0)
            {
                source.Seek(0, SeekOrigin.Begin);
            }
            await source.CopyToAsync(stream, cancellationToken);
        }
        stream.Position = 0;
        if (stream.Length == 0)
        {
            _logger.LogWarning(
                "Quarantine import: file stream is empty after read. FileName={FileName}, DeclaredLength={Length}",
                file.FileName, file.Length);
            return BadRequest(new { error = "The file could not be read. Ensure the file is a valid CSV and try again." });
        }

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
