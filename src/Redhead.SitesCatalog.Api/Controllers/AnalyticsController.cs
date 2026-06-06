using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Api.Models.Analytics;
using Redhead.SitesCatalog.Application.Models.Analytics;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Domain.Constants;

namespace Redhead.SitesCatalog.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Policy = AppPolicies.SuperAdminOnly)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IBusinessDemandAnalyticsService _analyticsService;

    public AnalyticsController(IBusinessDemandAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("business-demand")]
    public async Task<ActionResult<BusinessDemandAnalyticsDto>> GetBusinessDemand(
        [FromQuery] BusinessDemandAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var mapping = ToQuery(request);
        if (mapping.Error != null)
        {
            return BadRequest(new MessageResponse(mapping.Error));
        }

        var result = await _analyticsService.GetBusinessDemandAsync(
            mapping.Query!,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<AnalyticsClientOptionDto>>> GetClientOptions(
        CancellationToken cancellationToken)
    {
        var result = await _analyticsService.ListClientOptionsAsync(cancellationToken);
        return Ok(result);
    }

    private static BusinessDemandAnalyticsQueryMapping ToQuery(BusinessDemandAnalyticsRequest request)
    {
        var todayUtc = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = todayUtc.AddDays(-29);
        var toDate = todayUtc;

        if (!string.IsNullOrWhiteSpace(request.From) &&
            !TryParseDate(request.From, out fromDate))
        {
            return BusinessDemandAnalyticsQueryMapping.Invalid(
                "Invalid from date. Expected format: yyyy-MM-dd.");
        }

        if (!string.IsNullOrWhiteSpace(request.To) &&
            !TryParseDate(request.To, out toDate))
        {
            return BusinessDemandAnalyticsQueryMapping.Invalid(
                "Invalid to date. Expected format: yyyy-MM-dd.");
        }

        if (fromDate > toDate)
        {
            return BusinessDemandAnalyticsQueryMapping.Invalid(
                "From date must be earlier than or equal to to date.");
        }

        var destination = NormalizeDestination(request.Destination);
        if (destination.IsInvalid)
        {
            return BusinessDemandAnalyticsQueryMapping.Invalid(
                "Invalid destination. Allowed values: Download, GoogleDrive.");
        }

        var status = NormalizeStatus(request.Status);
        if (status.IsInvalid)
        {
            return BusinessDemandAnalyticsQueryMapping.Invalid(
                "Invalid status. Allowed values: successful, partial, blocked.");
        }

        return BusinessDemandAnalyticsQueryMapping.Valid(new BusinessDemandAnalyticsQuery
        {
            FromUtc = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ToUtc = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            ClientId = NormalizeOptionalText(request.ClientId),
            Destination = destination.Value,
            Status = status.Value
        });
    }

    private static bool TryParseDate(string value, out DateOnly date)
        => DateOnly.TryParseExact(
            value.Trim(),
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);

    private static NormalizedFilterValue NormalizeDestination(string? rawValue)
    {
        var value = NormalizeOptionalText(rawValue);
        if (value == null || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizedFilterValue.Valid(null);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "download" => NormalizedFilterValue.Valid(ExportConstants.DestinationDownload),
            "googledrive" or "google-drive" or "google drive" =>
                NormalizedFilterValue.Valid(ExportConstants.DestinationGoogleDrive),
            _ => NormalizedFilterValue.Invalid()
        };
    }

    private static NormalizedFilterValue NormalizeStatus(string? rawValue)
    {
        var value = NormalizeOptionalText(rawValue);
        if (value == null || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizedFilterValue.Valid(null);
        }

        return value.Trim().ToLowerInvariant() switch
        {
            BusinessDemandAnalyticsStatuses.Successful =>
                NormalizedFilterValue.Valid(BusinessDemandAnalyticsStatuses.Successful),
            BusinessDemandAnalyticsStatuses.Partial =>
                NormalizedFilterValue.Valid(BusinessDemandAnalyticsStatuses.Partial),
            BusinessDemandAnalyticsStatuses.Blocked =>
                NormalizedFilterValue.Valid(BusinessDemandAnalyticsStatuses.Blocked),
            _ => NormalizedFilterValue.Invalid()
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private sealed record BusinessDemandAnalyticsQueryMapping(
        BusinessDemandAnalyticsQuery? Query,
        string? Error)
    {
        public static BusinessDemandAnalyticsQueryMapping Valid(BusinessDemandAnalyticsQuery query)
            => new(query, null);

        public static BusinessDemandAnalyticsQueryMapping Invalid(string error)
            => new(null, error);
    }

    private sealed record NormalizedFilterValue(string? Value, bool IsInvalid)
    {
        public static NormalizedFilterValue Valid(string? value) => new(value, false);

        public static NormalizedFilterValue Invalid() => new(null, true);
    }
}
