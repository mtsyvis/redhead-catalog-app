using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace Redhead.SitesCatalog.Api.Middleware;

/// <summary>
/// Global exception handler for centralized error handling
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
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
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        var response = new
        {
            message = statusCode == HttpStatusCode.InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : exception.Message,
            statusCode = (int)statusCode
        };

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsync(
            JsonSerializer.Serialize(response),
            cancellationToken);

        return true;
    }
}
