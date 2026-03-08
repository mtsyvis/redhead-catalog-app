using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Api.Middleware;

/// <summary>
/// Global exception handler for centralized error handling
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "An unhandled exception occurred. Path: {Path}, Method: {Method}",
            httpContext.Request.Path,
            httpContext.Request.Method);

        var statusCode = exception switch
        {
            BadHttpRequestException badReq when badReq.StatusCode == StatusCodes.Status413PayloadTooLarge
                => HttpStatusCode.RequestEntityTooLarge,
            ImportHeaderValidationException => HttpStatusCode.BadRequest,
            ImportConcurrencyException => HttpStatusCode.Conflict,
            ExportDisabledException => HttpStatusCode.Forbidden,
            RoleSettingsNotFoundException => HttpStatusCode.InternalServerError,
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        var error = statusCode switch
        {
            HttpStatusCode.InternalServerError => "An unexpected error occurred. Please try again later.",
            HttpStatusCode.RequestEntityTooLarge => ImportConstants.FileTooLargeMessage,
            _ => exception.Message
        };

        var code = (int)statusCode;
        var traceId = statusCode == HttpStatusCode.InternalServerError ? httpContext.TraceIdentifier : null;
        var response = new ApiErrorResponse(error, code, traceId);

        httpContext.Response.StatusCode = code;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOptions),
            cancellationToken);

        return true;
    }
}
