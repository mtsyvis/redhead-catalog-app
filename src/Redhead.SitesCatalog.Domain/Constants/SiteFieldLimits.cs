namespace Redhead.SitesCatalog.Domain.Constants;

public static class SiteFieldLimits
{
    public const int DomainMaxLength = 255;
    public const int LocationMaxLength = 100;
    public const int LanguageMaxLength = 16;
    public const int SponsoredTagMaxLength = 100;
    public const int QuarantineReasonMaxLength = 1000;
    public const decimal MaxPriceUsd = 9_999_999_999_999_999.99m;
}
