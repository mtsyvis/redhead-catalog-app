using Redhead.SitesCatalog.Application.Models.Import;
using Redhead.SitesCatalog.Domain;

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
        decimal PriceUsd,
        decimal? PriceCasino,
        decimal? PriceCrypto,
        decimal? PriceLinkInsert,
        string? Niche,
        string? Categories);

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

        if (row.PriceUsd is null || row.PriceUsd < 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Price USD is required and must be >= 0."
            }, null);
        }

        if (string.IsNullOrWhiteSpace(row.DRRaw))
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "DR is required and must be between 0 and 100."
            }, null);
        }

        if (row.DR is null)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "DR",
                RawValue = row.DRRaw,
                Message = "Invalid numeric format for DR."
            }, null);
        }

        if (row.DR is < 0 or > 100)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Domain = domain,
                Field = "DR",
                RawValue = row.DRRaw,
                Message = "DR must be between 0 and 100."
            }, null);
        }

        if (row.Traffic is null || row.Traffic < 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "Traffic is required and must be >= 0."
            }, null);
        }

        if (row.PriceCasino is not null && row.PriceCasino < 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceCasino must be >= 0 or empty."
            }, null);
        }

        if (row.PriceCrypto is not null && row.PriceCrypto < 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceCrypto must be >= 0 or empty."
            }, null);
        }

        if (row.PriceLinkInsert is not null && row.PriceLinkInsert < 0)
        {
            return (false, new SitesImportError
            {
                RowNumber = row.RowNumber,
                Message = "PriceLinkInsert must be >= 0 or empty."
            }, null);
        }

        var data = new ValidatedSitesRow(
            domain,
            row.DR ?? 0,
            row.Traffic ?? 0,
            (row.Location ?? string.Empty).Trim(),
            row.PriceUsd ?? 0,
            row.PriceCasino,
            row.PriceCrypto,
            row.PriceLinkInsert,
            string.IsNullOrWhiteSpace(row.Niche) ? null : row.Niche.Trim(),
            string.IsNullOrWhiteSpace(row.Categories) ? null : row.Categories.Trim());

        return (false, null, data);
    }

    public static bool IsEmptyRow(SitesImportRowDto row)
    {
        return string.IsNullOrWhiteSpace(row.Domain)
               && string.IsNullOrWhiteSpace(row.DRRaw)
               && row.Traffic is null
               && string.IsNullOrWhiteSpace(row.Location)
               && row.PriceUsd is null
               && row.PriceCasino is null
               && row.PriceCrypto is null
               && row.PriceLinkInsert is null
               && string.IsNullOrWhiteSpace(row.Niche)
               && string.IsNullOrWhiteSpace(row.Categories);
    }
}
