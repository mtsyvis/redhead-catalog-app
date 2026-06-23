namespace Redhead.SitesCatalog.Domain.Constants;

public static class UserAccountStatuses
{
    public const string Active = "Active";
    public const string PendingActivation = "PendingActivation";
    public const string InvitationExpired = "InvitationExpired";
    public const string Disabled = "Disabled";

    public static string Resolve(
        bool isActive,
        DateTime? activatedAtUtc,
        DateTime? invitationExpiresAtUtc,
        DateTime utcNow)
    {
        if (!isActive)
        {
            return Disabled;
        }

        if (activatedAtUtc.HasValue)
        {
            return Active;
        }

        return invitationExpiresAtUtc.HasValue && invitationExpiresAtUtc.Value > utcNow
            ? PendingActivation
            : InvitationExpired;
    }
}
