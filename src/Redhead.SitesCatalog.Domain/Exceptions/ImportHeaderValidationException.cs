namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// Thrown when the import file header does not have required columns or correct sequence.
/// </summary>
public class ImportHeaderValidationException : Exception
{
    public ImportHeaderValidationException(string message)
        : base(message)
    {
    }
}
