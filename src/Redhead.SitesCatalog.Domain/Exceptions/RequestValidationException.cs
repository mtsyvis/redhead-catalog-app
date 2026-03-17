namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when a request contains invalid user-provided input.
/// Mapped to HTTP 400 Bad Request.
/// </summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string message)
        : base(message)
    {
    }

    public RequestValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
