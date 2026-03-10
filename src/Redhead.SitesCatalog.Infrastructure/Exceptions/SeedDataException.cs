namespace Redhead.SitesCatalog.Infrastructure.Exceptions;

/// <summary>
/// Represents errors that occur during the seeding of application data.
/// </summary>
/// <remarks>This exception is typically thrown when an error is encountered while initializing or populating a
/// data store with seed data. Use this exception to distinguish seeding-related failures from other types of
/// exceptions.</remarks>
public class SeedDataException : Exception
{
    public SeedDataException(string message) : base(message)
    {
    }

    public SeedDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
