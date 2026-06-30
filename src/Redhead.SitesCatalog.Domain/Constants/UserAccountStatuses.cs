namespace Redhead.SitesCatalog.Domain.Constants;

public static class UserAccountStatuses
{
    public const string Active = "Active";
    public const string PendingActivation = "PendingActivation";
    public const string InvitationExpired = "InvitationExpired";
    public const string PendingReactivation = "PendingReactivation";
    public const string ReactivationExpired = "ReactivationExpired";
    public const string Disabled = "Disabled";

    public static string Resolve(
        bool isActive,
        DateTime? activatedAtUtc,
        string? invitationTokenHash,
        DateTime? invitationExpiresAtUtc,
        DateTime utcNow)
    {
        if (!isActive)
        {
            if (activatedAtUtc.HasValue && !string.IsNullOrWhiteSpace(invitationTokenHash))
            {
                return invitationExpiresAtUtc.HasValue && invitationExpiresAtUtc.Value > utcNow
                    ? PendingReactivation
                    : ReactivationExpired;
            }

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
