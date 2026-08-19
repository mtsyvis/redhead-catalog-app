using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Application.Validation;

public static class WebmasterOfferWriteValidator
{
    public static WebmasterOfferWriteValidationResult ValidateAndNormalize(UpdateWebmasterOfferRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request.ExpectedUpdatedAtUtc == default)
        {
            AddError(errors, "expectedUpdatedAtUtc", "The offer version is required. Reload the offer and try again.");
        }

        if (!Enum.IsDefined(request.Status))
        {
            AddError(errors, "status", "Status is invalid.");
        }

        ValidateTerm(request, errors);
        ValidateLength(request.OutreachSenderRawText, WebmasterOfferFieldLimits.RawTextMaxLength, "outreachSenderRawText", errors);
        ValidateLength(request.LinkPolicyText, WebmasterOfferFieldLimits.RawTextMaxLength, "linkPolicyText", errors);
        ValidateLength(request.DfLinksRawText, WebmasterOfferFieldLimits.RawTextMaxLength, "dfLinksRawText", errors);
        ValidateLength(request.SponsoredTagRawText, WebmasterOfferFieldLimits.RawTextMaxLength, "sponsoredTagRawText", errors);
        ValidateLength(request.CommentText, WebmasterOfferFieldLimits.RawTextMaxLength, "commentText", errors);
        ValidateLength(request.ClientRawText, WebmasterOfferFieldLimits.RawTextMaxLength, "clientRawText", errors);

        var duplicateMailboxIds = request.LinkbuilderMailboxIds
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicateMailboxIds.Count > 0)
        {
            AddError(errors, "linkbuilderMailboxIds", "Linkbuilder mailboxes must be unique.");
        }

        var duplicatePriceTypes = request.Prices
            .GroupBy(price => price.PriceType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();
        if (duplicatePriceTypes.Count > 0)
        {
            AddError(errors, "prices", "Only one row per webmaster price type is allowed.");
        }

        foreach (var price in request.Prices)
        {
            ValidatePrice(price, errors);
        }

        if (errors.Count > 0)
        {
            return new WebmasterOfferWriteValidationResult
            {
                FieldErrors = errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase)
            };
        }

        var normalizedPrices = request.Prices
            .Where(price => price.AvailabilityStatus != ServiceAvailabilityStatus.Unknown
                || price.WebmasterPriceUsd.HasValue
                || !string.IsNullOrWhiteSpace(price.WebmasterPriceDetails))
            .Select(price => new UpdateWebmasterOfferPriceRequest
            {
                PriceType = price.PriceType,
                AvailabilityStatus = price.AvailabilityStatus,
                WebmasterPriceUsd = price.AvailabilityStatus == ServiceAvailabilityStatus.Available
                    ? price.WebmasterPriceUsd
                    : null,
                WebmasterPriceDetails = TrimToNull(price.WebmasterPriceDetails)
            })
            .ToList();

        return new WebmasterOfferWriteValidationResult
        {
            NormalizedRequest = new UpdateWebmasterOfferRequest
            {
                ExpectedUpdatedAtUtc = request.ExpectedUpdatedAtUtc,
                Status = request.Status,
                OutreachSenderRawText = TrimToNull(request.OutreachSenderRawText),
                LinkPolicyText = TrimToNull(request.LinkPolicyText),
                DfLinksRawText = TrimToNull(request.DfLinksRawText),
                SponsoredTagRawText = TrimToNull(request.SponsoredTagRawText),
                CommentText = TrimToNull(request.CommentText),
                ClientRawText = TrimToNull(request.ClientRawText),
                TermType = request.TermType,
                TermValue = request.TermValue,
                TermUnit = request.TermUnit,
                LinkbuilderMailboxIds = request.LinkbuilderMailboxIds.Distinct().ToList(),
                Prices = normalizedPrices
            }
        };
    }

    private static void ValidateTerm(
        UpdateWebmasterOfferRequest request,
        IDictionary<string, List<string>> errors)
    {
        var valid = request.TermType switch
        {
            null => request.TermValue is null && request.TermUnit is null,
            TermType.Permanent => request.TermValue is null && request.TermUnit is null,
            TermType.Finite => request.TermValue is > 0 && request.TermUnit == TermUnit.Year,
            _ => false
        };

        if (!valid)
        {
            AddError(errors, "term", "Term must be No term, Permanent, or a positive number of years.");
        }
    }

    private static void ValidatePrice(
        UpdateWebmasterOfferPriceRequest price,
        IDictionary<string, List<string>> errors)
    {
        var prefix = $"prices.{(short)price.PriceType}";
        if (!Enum.IsDefined(price.PriceType))
        {
            AddError(errors, "prices", "Price type is invalid.");
            return;
        }

        if (!Enum.IsDefined(price.AvailabilityStatus))
        {
            AddError(errors, $"{prefix}.availabilityStatus", "Availability is invalid.");
            return;
        }

        if (price.PriceType == WebmasterOfferPriceType.Main
            && price.AvailabilityStatus is ServiceAvailabilityStatus.NotAvailable or ServiceAvailabilityStatus.AvailableWithUnknownPrice)
        {
            AddError(errors, $"{prefix}.availabilityStatus", "Main price supports only Numeric price or Unknown.");
        }

        if (price.AvailabilityStatus == ServiceAvailabilityStatus.Available)
        {
            if (price.WebmasterPriceUsd is null or <= 0)
            {
                AddError(errors, $"{prefix}.webmasterPriceUsd", "Price must be greater than 0.");
            }
            else if (price.WebmasterPriceUsd > WebmasterOfferFieldLimits.MaxPriceUsd)
            {
                AddError(errors, $"{prefix}.webmasterPriceUsd", "Price is too large.");
            }
            else if (decimal.Round(price.WebmasterPriceUsd.Value, 2) != price.WebmasterPriceUsd.Value)
            {
                AddError(errors, $"{prefix}.webmasterPriceUsd", "Price may have at most 2 decimal places.");
            }
        }
        else if (price.WebmasterPriceUsd.HasValue)
        {
            AddError(errors, $"{prefix}.webmasterPriceUsd", "Price must be empty unless availability is Numeric price.");
        }

        ValidateLength(
            price.WebmasterPriceDetails,
            WebmasterOfferFieldLimits.PriceDetailsMaxLength,
            $"{prefix}.webmasterPriceDetails",
            errors);
    }

    private static void ValidateLength(
        string? value,
        int maxLength,
        string field,
        IDictionary<string, List<string>> errors)
    {
        if (value?.Length > maxLength)
        {
            AddError(errors, field, $"Value must be {maxLength:N0} characters or fewer.");
        }
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class WebmasterOfferWriteValidationResult
{
    public UpdateWebmasterOfferRequest? NormalizedRequest { get; init; }
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; init; }
        = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    public bool IsValid => NormalizedRequest is not null;
}
