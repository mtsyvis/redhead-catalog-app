using Microsoft.AspNetCore.Diagnostics;
using Redhead.SitesCatalog.Api.Models;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Exceptions;
using System.Net;
using System.Text;
using System.Text.Json;

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

        var (statusCode, error) = MapException(exception);

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
    private static (HttpStatusCode StatusCode, string Error) MapException(Exception exception)
    {
        return exception switch
        {
            BadHttpRequestException badReq when badReq.StatusCode == StatusCodes.Status413PayloadTooLarge
                => (HttpStatusCode.RequestEntityTooLarge, ImportConstants.FileTooLargeMessage),

            ImportHeaderValidationException ex
                => (HttpStatusCode.BadRequest, ex.Message),

            DecoderFallbackException
                => (HttpStatusCode.BadRequest, "CSV must be UTF-8 encoded."),

            ImportConcurrencyException ex
                => (HttpStatusCode.Conflict, ex.Message),

            ExportDisabledException ex
                => (HttpStatusCode.Forbidden, ex.Message),

            RoleSettingsNotFoundException
                => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later."),

            RequestValidationException ex
                => (HttpStatusCode.BadRequest, ex.Message),

            UnauthorizedAccessException ex
                => (HttpStatusCode.Unauthorized, ex.Message),

            KeyNotFoundException ex
                => (HttpStatusCode.NotFound, ex.Message),

            SeedDataException ex 
                => (HttpStatusCode.InternalServerError, ex.Message),

            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };
    }
}
