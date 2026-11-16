using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public sealed record BulkOfferPriceApplyRequest(Guid OperationId, BulkOfferPricePreviewRequest Input, string PreviewVersion);
public sealed record BulkOfferPriceOperationResult(Guid Id, string Actor, DateTime AppliedAtUtc,
    IReadOnlyList<WebmasterOfferPriceType> PriceTypes, BulkOfferPricePreviewResult Result);
public sealed record BulkOfferPriceOperationSummary(Guid Id, string Actor, DateTime AppliedAtUtc, string PrimaryEmail, int UpdatedCount, int NoChangesCount, int ExceptionCount);

// Immutable factual input for Slice 3. Result reads never recalculate these values.
public sealed record BulkOfferPurchaseMinimum(string Domain, PriceType Service, PricingTerm Term,
    decimal? BeforeUsd, decimal? AfterUsd, bool IsQuarantined);
public sealed record BulkOfferSavedPrice(WebmasterOfferPriceType PriceType, PricingTerm Term,
    BulkOfferPriceValue Value, string? Details);
public sealed record BulkOfferSavedOffer(Guid OfferId, IReadOnlyList<BulkOfferSavedPrice> Before,
    IReadOnlyList<BulkOfferSavedPrice> After);
public sealed record BulkOfferPriceOperationSnapshot(BulkOfferPriceOperationResult Operation,
    BulkOfferPricePreviewRequest Input, IReadOnlyList<BulkOfferSavedOffer> Offers,
    IReadOnlyList<BulkOfferPurchaseMinimum> Minima);
