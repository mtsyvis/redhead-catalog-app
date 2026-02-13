namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// Exception thrown when export is disabled for a role (ExportLimitRows = 0)
/// </summary>
public class ExportDisabledException : Exception
{
    public string RoleName { get; }

    public ExportDisabledException(string roleName)
        : base($"Export is disabled for role: {roleName}")
    {
        RoleName = roleName;
    }

    public ExportDisabledException(string roleName, string message)
        : base(message)
    {
        RoleName = roleName;
    }
}
