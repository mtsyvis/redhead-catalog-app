namespace Redhead.SitesCatalog.Domain.Invitations;

public static class InvitationPolicy
{
    public const int LifetimeHours = 24;

    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(LifetimeHours);
}
