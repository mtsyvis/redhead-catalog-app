using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Services.Parsers;

/// <summary>
/// Shared pure validation for sites import rows. Used by both add-only and update imports.
/// One source of truth for row validation rules.
/// </summary>
public static class SitesImportRowValidationHelper
{
    /// <summary>
    /// Validated row data usable for insert or update. Domain is the normalized lookup key.
    /// </summary>
    public sealed record ValidatedSitesRow(
        string NormalizedDomain,
        double DR,
        long Traffic,
        string Location,
        decimal? PriceUsd,
        decimal? PriceCasino,
        ServiceAvailabilityStatus PriceCasinoStatus,
        decimal? PriceCrypto,
        ServiceAvailabilityStatus PriceCryptoStatus,
        decimal? PriceLinkInsert,
        ServiceAvailabilityStatus PriceLinkInsertStatus,
        decimal? PriceLinkInsertCasino,
        ServiceAvailabilityStatus PriceLinkInsertCasinoStatus,
        decimal? PriceDating,
        ServiceAvailabilityStatus PriceDatingStatus,
        int? NumberDFLinks,
        TermType? TermType,
        int? TermValue,
        TermUnit? TermUnit,
        string? Niche,
        string? Categories,
        string? LinkType,
        string? SponsoredTag);

    /// <summary>
    /// Result of validating a row: empty (skip), error, or valid data.
    /// </summary>
    public static (bool IsEmpty, SitesImportError? Error, ValidatedSitesRow? Data) Validate(SitesImportRowDto row)
    {
        if (IsEmptyRow(row))
        {
            return (true, null, null);
        }

        if (string.IsNullOrWhiteSpace(row.Domain))
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain is required."
            }, null);
        }

        var domain = DomainNormalizer.Normalize(row.Domain);
        if (string.IsNullOrEmpty(domain))
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Domain could not be normalized."
            }, null);
        }

        if (!string.IsNullOrWhiteSpace(row.PriceUsdRaw) && row.PriceUsd is null)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceUsd",
                RawValue = row.PriceUsdRaw,
                Message = "Invalid PriceUsd value."
            }, null);
        }

        var casinoParseResult = OptionalServiceValueParser.Parse(row.PriceCasinoRaw);
        if (!casinoParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceCasino",
                RawValue = row.PriceCasinoRaw,
                Message = casinoParseResult.ErrorMessage ?? "Invalid PriceCasino value."
            }, null);
        }

        var cryptoParseResult = OptionalServiceValueParser.Parse(row.PriceCryptoRaw);
        if (!cryptoParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceCrypto",
                RawValue = row.PriceCryptoRaw,
                Message = cryptoParseResult.ErrorMessage ?? "Invalid PriceCrypto value."
            }, null);
        }

        var linkInsertParseResult = OptionalServiceValueParser.Parse(row.PriceLinkInsertRaw);
        if (!linkInsertParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceLinkInsert",
                RawValue = row.PriceLinkInsertRaw,
                Message = linkInsertParseResult.ErrorMessage ?? "Invalid PriceLinkInsert value."
            }, null);
        }

        var linkInsertCasinoParseResult = OptionalServiceValueParser.Parse(row.PriceLinkInsertCasinoRaw);
        if (!linkInsertCasinoParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceLinkInsertCasino",
                RawValue = row.PriceLinkInsertCasinoRaw,
                Message = linkInsertCasinoParseResult.ErrorMessage ?? "Invalid PriceLinkInsertCasino value."
            }, null);
        }

        var datingParseResult = OptionalServiceValueParser.Parse(row.PriceDatingRaw);
        if (!datingParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "PriceDating",
                RawValue = row.PriceDatingRaw,
                Message = datingParseResult.ErrorMessage ?? "Invalid PriceDating value."
            }, null);
        }

        if (!string.IsNullOrWhiteSpace(row.NumberDFLinksRaw) && row.NumberDFLinks is null)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "NumberDFLinks",
                RawValue = row.NumberDFLinksRaw,
                Message = "Invalid NumberDFLinks value."
            }, null);
        }

        var termParseResult = TermValueParser.Parse(row.TermRaw);
        if (!termParseResult.IsValid)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "Term",
                RawValue = row.TermRaw,
                Message = termParseResult.ErrorMessage ?? "Invalid Term value."
            }, null);
        }

        var writeValidationResult = SiteWriteValidator.ValidateAndNormalize(new SiteWriteInput
        {
            DR = row.DR,
            Traffic = row.Traffic,
            Location = row.Location,
            PriceUsd = row.PriceUsd,
            PriceCasino = casinoParseResult.Price,
            PriceCasinoStatus = casinoParseResult.Status,
            PriceCrypto = cryptoParseResult.Price,
            PriceCryptoStatus = cryptoParseResult.Status,
            PriceLinkInsert = linkInsertParseResult.Price,
            PriceLinkInsertStatus = linkInsertParseResult.Status,
            PriceLinkInsertCasino = linkInsertCasinoParseResult.Price,
            PriceLinkInsertCasinoStatus = linkInsertCasinoParseResult.Status,
            PriceDating = datingParseResult.Price,
            PriceDatingStatus = datingParseResult.Status,
            NumberDFLinks = row.NumberDFLinks,
            TermType = termParseResult.TermType,
            TermValue = termParseResult.TermValue,
            TermUnit = termParseResult.TermUnit,
            Niche = row.Niche,
            Categories = row.Categories,
            LinkType = row.LinkType,
            SponsoredTag = row.SponsoredTag,
            IsQuarantined = false,
            QuarantineReason = null
        });

        if (!writeValidationResult.IsValid)
        {
            var allMessages = writeValidationResult.FieldErrors
                .SelectMany(kv => kv.Value)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
            var combinedMessage = allMessages.Length > 0
                ? string.Join("; ", allMessages)
                : "Row validation failed.";

            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Message = combinedMessage
            }, null);
        }

        var normalized = writeValidationResult.NormalizedRequest;
        if (normalized is null)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Message = "Row validation failed."
            }, null);
        }

        var data = new ValidatedSitesRow(
            domain,
            normalized.DR,
            normalized.Traffic,
            normalized.Location,
            normalized.PriceUsd,
            normalized.PriceCasino,
            normalized.PriceCasinoStatus,
            normalized.PriceCrypto,
            normalized.PriceCryptoStatus,
            normalized.PriceLinkInsert,
            normalized.PriceLinkInsertStatus,
            normalized.PriceLinkInsertCasino,
            normalized.PriceLinkInsertCasinoStatus,
            normalized.PriceDating,
            normalized.PriceDatingStatus,
            normalized.NumberDFLinks,
            normalized.TermType,
            normalized.TermValue,
            normalized.TermUnit,
            normalized.Niche,
            normalized.Categories,
            normalized.LinkType,
            normalized.SponsoredTag);

        return (false, null, data);
    }

    public static bool IsEmptyRow(SitesImportRowDto row)
    {
        return string.IsNullOrWhiteSpace(row.Domain)
               && string.IsNullOrWhiteSpace(row.DRRaw)
               && row.Traffic is null
               && string.IsNullOrWhiteSpace(row.Location)
               && string.IsNullOrWhiteSpace(row.PriceUsdRaw)
               && string.IsNullOrWhiteSpace(row.PriceCasinoRaw)
               && string.IsNullOrWhiteSpace(row.PriceCryptoRaw)
               && string.IsNullOrWhiteSpace(row.PriceLinkInsertRaw)
               && string.IsNullOrWhiteSpace(row.PriceLinkInsertCasinoRaw)
               && string.IsNullOrWhiteSpace(row.PriceDatingRaw)
               && string.IsNullOrWhiteSpace(row.NumberDFLinksRaw)
               && string.IsNullOrWhiteSpace(row.TermRaw)
               && string.IsNullOrWhiteSpace(row.Niche)
               && string.IsNullOrWhiteSpace(row.Categories)
               && string.IsNullOrWhiteSpace(row.LinkType)
               && string.IsNullOrWhiteSpace(row.SponsoredTag);
    }

}
