namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// A concurrent offer change prevented the site and offer quarantine updates from being saved.
/// </summary>
public sealed class QuarantineConcurrencyException : Exception
{
    public QuarantineConcurrencyException(Exception innerException)
        : base("The data was changed by another user. Refresh the data and try again.", innerException)
    {
    }
}
