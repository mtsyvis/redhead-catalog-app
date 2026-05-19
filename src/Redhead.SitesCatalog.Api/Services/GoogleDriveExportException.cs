namespace Redhead.SitesCatalog.Api.Services;

public sealed class GoogleDriveExportException : Exception
{
    private GoogleDriveExportException(string errorCode, int statusCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    public string ErrorCode { get; }
    public int StatusCode { get; }

    public static GoogleDriveExportException NotConnected()
        => new(
            "GoogleDriveNotConnected",
            StatusCodes.Status409Conflict,
            "Connect Google Drive before saving exports.");

    public static GoogleDriveExportException ReconnectRequired()
        => new(
            "GoogleDriveReconnectRequired",
            StatusCodes.Status409Conflict,
            "Reconnect Google Drive before saving exports.");

    public static GoogleDriveExportException ConfigurationMissing()
        => new(
            "GoogleDriveConfigurationMissing",
            StatusCodes.Status500InternalServerError,
            "Google Drive export is not configured.");

    public static GoogleDriveExportException UploadFailed()
        => new(
            "GoogleDriveUploadFailed",
            StatusCodes.Status502BadGateway,
            "Google Drive export could not be uploaded.");
}

