namespace Redhead.SitesCatalog.Application.Services.Analytics;

internal static class ExportAnalyticsSnapshotSchema
{
    internal static class Filters
    {
        public const string Dr = "dr";
        public const string Traffic = "traffic";
        public const string PriceUsd = "priceUsd";
        public const string Location = "location";
        public const string LocationKey = "locationKey";
        public const string LocationGroup = "locationGroup";
        public const string ExcludedLocationKey = "excludedLocationKey";
        public const string LocationUnknown = "locationUnknown";
        public const string LocationOther = "locationOther";
        public const string Language = "language";
        public const string Niche = "niche";
        public const string Categories = "categories";
        public const string ExcludedNiche = "excludedNiche";
        public const string ExcludedCategories = "excludedCategories";
        public const string TopicFitMode = "topicFitMode";
        public const string Quarantine = "quarantine";
        public const string LastPublishedDate = "lastPublishedDate";
        public const string StopList = "stopList";
        public const string PriceCasinoAvailability = "priceCasinoAvailability";
        public const string PriceCryptoAvailability = "priceCryptoAvailability";
        public const string PriceLinkInsertAvailability = "priceLinkInsertAvailability";
        public const string PriceLinkInsertCasinoAvailability = "priceLinkInsertCasinoAvailability";
        public const string PriceDatingAvailability = "priceDatingAvailability";
    }

    internal static class Sort
    {
        public const string Domain = "domain";
        public const string Dr = "dr";
        public const string Traffic = "traffic";
        public const string Location = "location";
        public const string PriceUsd = "priceUsd";
        public const string PriceCasino = "priceCasino";
        public const string PriceCrypto = "priceCrypto";
        public const string PriceLinkInsert = "priceLinkInsert";
        public const string PriceLinkInsertCasino = "priceLinkInsertCasino";
        public const string PriceDating = "priceDating";
        public const string NumberDfLinks = "numberDFLinks";
        public const string Term = "term";
        public const string CreatedAt = "createdAt";
        public const string UpdatedAt = "updatedAt";
        public const string LastPublishedDate = "lastPublishedDate";
    }

    internal static class Search
    {
        public const string Mode = "mode";
        public const string Query = "query";
        public const string NormalizedQuery = "normalizedQuery";
        public const string CatalogSearchMode = "catalogSearch";
        public const string MultiSearchMode = "multiSearch";
        public const string InputCount = "inputCount";
        public const string UniqueInputCount = "uniqueInputCount";
        public const string FoundCount = "foundCount";
        public const string NotFoundCount = "notFoundCount";
    }
}
