namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveApiException : Exception
{
    public GoogleDriveApiException(string message, bool reconnectRequired = false)
        : base(message)
    {
        ReconnectRequired = reconnectRequired;
    }

    public bool ReconnectRequired { get; }
}

