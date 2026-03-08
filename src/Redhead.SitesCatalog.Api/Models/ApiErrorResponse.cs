namespace Redhead.SitesCatalog.Api.Models;

/// <summary>
/// Standard API error response shape. Includes "message" as alias of "error" for backward compatibility.
/// </summary>
public sealed record ApiErrorResponse(string Error, int StatusCode, string? TraceId = null)
{
    /// <summary>
    /// Backward-compatible alias for Error (same value).
    /// </summary>
    public string Message => Error;
}
