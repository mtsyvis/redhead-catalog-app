namespace Redhead.SitesCatalog.Domain.Constants;

public static class EntityChangeHistoryConstants
{
    public const string SiteEntityType = "Site";
    public const string WebmasterOfferEntityType = "SiteWebmasterOffer";
    public const string UpdatedAction = "Updated";
    public const string ManualSource = "Manual";

    public const int EntityTypeMaxLength = 64;
    public const int EntityIdMaxLength = 320;
    public const int ActionMaxLength = 32;
    public const int SourceMaxLength = 64;
    public const int ChangedByMaxLength = 320;
}
