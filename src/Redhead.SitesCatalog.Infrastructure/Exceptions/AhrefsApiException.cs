namespace Redhead.SitesCatalog.Infrastructure.Exceptions;

public sealed class AhrefsApiException : Exception
{
    public AhrefsApiException(
        string message,
        int? statusCode = null,
        long? actualUnits = null,
        bool batchResponseReceived = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ActualUnits = actualUnits;
        BatchResponseReceived = batchResponseReceived;
    }

    public int? StatusCode { get; }
    public long? ActualUnits { get; }
    public bool BatchResponseReceived { get; }
}
