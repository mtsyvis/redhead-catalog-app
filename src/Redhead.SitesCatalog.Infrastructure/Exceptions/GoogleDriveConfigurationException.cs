namespace Redhead.SitesCatalog.Infrastructure.Exceptions;

public sealed class GoogleDriveConfigurationException : Exception
{
    public GoogleDriveConfigurationException(string message)
        : base(message)
    {
    }
}
