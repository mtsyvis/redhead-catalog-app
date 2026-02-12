using Microsoft.AspNetCore.Identity;

namespace Redhead.SitesCatalog.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = true;
}
