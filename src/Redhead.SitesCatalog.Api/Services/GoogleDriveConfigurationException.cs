namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveConfigurationException : Exception
{
    public GoogleDriveConfigurationException(string message)
        : base(message)
    {
    }
}
