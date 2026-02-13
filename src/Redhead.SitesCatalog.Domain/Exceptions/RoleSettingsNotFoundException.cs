namespace Redhead.SitesCatalog.Domain.Exceptions;

/// <summary>
/// Exception thrown when role settings are not found for a role
/// </summary>
public class RoleSettingsNotFoundException : Exception
{
    public string RoleName { get; }

    public RoleSettingsNotFoundException(string roleName)
        : base($"Role settings not found for role: {roleName}")
    {
        RoleName = roleName;
    }

    public RoleSettingsNotFoundException(string roleName, string message)
        : base(message)
    {
        RoleName = roleName;
    }
}
