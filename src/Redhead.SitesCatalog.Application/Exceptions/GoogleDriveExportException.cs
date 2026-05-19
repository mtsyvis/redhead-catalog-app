namespace Redhead.SitesCatalog.Application.Exceptions;

public sealed class GoogleDriveExportException : Exception
{
    public const string NotConnectedErrorCode = "GoogleDriveNotConnected";
    public const string ReconnectRequiredErrorCode = "GoogleDriveReconnectRequired";
    public const string ConfigurationMissingErrorCode = "GoogleDriveConfigurationMissing";
    public const string UploadFailedErrorCode = "GoogleDriveUploadFailed";

    private GoogleDriveExportException(string errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }

    public static GoogleDriveExportException NotConnected()
        => new(
            NotConnectedErrorCode,
            "Connect Google Drive before saving exports.");

    public static GoogleDriveExportException ReconnectRequired()
        => new(
            ReconnectRequiredErrorCode,
            "Reconnect Google Drive before saving exports.");

    public static GoogleDriveExportException ConfigurationMissing()
        => new(
            ConfigurationMissingErrorCode,
            "Google Drive export is not configured.");

    public static GoogleDriveExportException UploadFailed()
        => new(
            UploadFailedErrorCode,
            "Google Drive export could not be uploaded.");
}
