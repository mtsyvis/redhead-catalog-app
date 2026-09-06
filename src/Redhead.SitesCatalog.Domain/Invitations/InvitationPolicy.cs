namespace Redhead.SitesCatalog.Domain.Invitations;

public static class InvitationPolicy
{
    public const int LifetimeHours = 48;

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(LifetimeHours);
}
