namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when an import operation conflicts due to concurrent modification (e.g. optimistic concurrency).
/// Mapped to HTTP 409 Conflict.
/// </summary>
public sealed class ImportConcurrencyException : Exception
{
    public ImportConcurrencyException(string message)
        : base(message)
    {
    }

    public ImportConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
