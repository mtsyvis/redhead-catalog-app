using System.Text.Json.Serialization;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;
using Redhead.SitesCatalog.Application.Services.WebmasterOffers;

namespace Redhead.SitesCatalog.Application.Models.WebmasterOffers;

public static class BulkOfferPricePreviewOutcomes
{
    public const string Ready = "Ready";
    public const string NoChanges = "No changes";
    public const string Exceptions = "Exceptions";
}

public sealed record BulkOfferPricePreviewRequest(
    string PrimaryEmail, TermType? TermType, int? TermValue, TermUnit? TermUnit,
    string SourceCurrency, IReadOnlyList<WebmasterOfferPriceType> PriceTypes,
    IReadOnlyList<BulkOfferPriceInputRow> Rows);

public sealed record BulkOfferPriceInputRow(int RowNumber, string Domain, IReadOnlyList<string> Values);

public sealed record BulkOfferPriceValue(
    ServiceAvailabilityStatus AvailabilityStatus,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? WebmasterPriceUsd,
    string SourceCurrency,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? SourceAmount,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)] decimal? AppliedUsdRate,
    DateOnly? RateDate, decimal AppliedUsdSurcharge = 0m)
{
    internal static BulkOfferPriceValue FromPrice(WebmasterOfferPrice price) => new(price.AvailabilityStatus,
        price.WebmasterPriceUsd, price.SourceCurrency, price.SourceAmount, price.AppliedUsdRate,
        price.RateDate, price.AppliedUsdSurcharge);
}

public sealed record BulkOfferPriceChange(WebmasterOfferPriceType PriceType,
    BulkOfferPriceValue? Current, BulkOfferPriceValue Proposed, bool Changed);

public sealed record BulkOfferPricePreviewRow(
    string Domain, bool InvalidDomain, IReadOnlyList<BulkOfferPriceInputRow> Inputs,
    string Outcome, string? Category, IReadOnlyList<string> Reasons,
    Guid? OfferId, string? CurrentTerm, string? ProposedTerm,
    SiteWebmasterOfferStatus? CurrentStatus, SiteWebmasterOfferStatus? ProposedStatus,
    IReadOnlyList<BulkOfferPriceChange> Prices);

public sealed record BulkOfferPricePreviewResult(
    string PrimaryEmail, string Term, string SourceCurrency, int InputCount, int DuplicateInputCount,
    int ReadyCount, int NoChangesCount, int ExceptionCount,
    IReadOnlyList<BulkOfferPricePreviewRow> Rows, OfferCurrencyReference? ConversionReference,
    string PreviewVersion = "");
