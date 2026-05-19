namespace Redhead.SitesCatalog.Application.Exceptions;

public sealed class GoogleDriveIntegrationException : Exception
{
    public GoogleDriveIntegrationException(string message)
        : base(message)
    {
    }

    public GoogleDriveIntegrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
