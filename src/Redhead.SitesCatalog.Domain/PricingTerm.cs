using TermKind = Redhead.SitesCatalog.Domain.Enums.TermType;
using TermUnitKind = Redhead.SitesCatalog.Domain.Enums.TermUnit;

namespace Redhead.SitesCatalog.Domain;

public sealed record PricingTerm(
    string TermKey,
    TermKind? TermType,
    int? TermValue,
    TermUnitKind? TermUnit)
{
    public const string UnknownKey = "unknown";
    public const string PermanentKey = "permanent";

    public string DisplayLabel => FormatLabel(TermKey, TermType, TermValue, TermUnit);

    public static PricingTerm Unknown { get; } = new(UnknownKey, null, null, null);

    public static PricingTerm Permanent { get; } = new(PermanentKey, TermKind.Permanent, null, null);

    public static PricingTerm FiniteYears(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Finite term value must be greater than zero.");
        }

        return new PricingTerm($"finite:{value}:year", TermKind.Finite, value, TermUnitKind.Year);
    }

    public static PricingTerm FromTerm(TermKind? termType, int? termValue, TermUnitKind? termUnit)
    {
        if (termType == TermKind.Permanent && termValue is null && termUnit is null)
        {
            return Permanent;
        }

        if (termType == TermKind.Finite && termValue is > 0 && termUnit == TermUnitKind.Year)
        {
            return FiniteYears(termValue.Value);
        }

        return Unknown;
    }

    public static bool TryCreate(
        string? termKey,
        TermKind? termType,
        int? termValue,
        TermUnitKind? termUnit,
        out PricingTerm pricingTerm)
    {
        var normalizedTermKey = termKey?.Trim();
        if (string.IsNullOrWhiteSpace(termKey))
        {
            pricingTerm = Unknown;
            return false;
        }

        if (string.Equals(normalizedTermKey, UnknownKey, StringComparison.Ordinal)
            && termType is null
            && termValue is null
            && termUnit is null)
        {
            pricingTerm = Unknown;
            return true;
        }

        if (string.Equals(normalizedTermKey, PermanentKey, StringComparison.Ordinal)
            && termType == TermKind.Permanent
            && termValue is null
            && termUnit is null)
        {
            pricingTerm = Permanent;
            return true;
        }

        if (termType == TermKind.Finite
            && termValue is > 0
            && termUnit == TermUnitKind.Year
            && string.Equals(normalizedTermKey, $"finite:{termValue.Value}:year", StringComparison.Ordinal))
        {
            pricingTerm = FiniteYears(termValue.Value);
            return true;
        }

        pricingTerm = new PricingTerm(normalizedTermKey, termType, termValue, termUnit);
        return false;
    }

    public static string FormatLabel(
        string termKey,
        TermKind? termType,
        int? termValue,
        TermUnitKind? termUnit)
    {
        if (termType == TermKind.Permanent || string.Equals(termKey, PermanentKey, StringComparison.Ordinal))
        {
            return "Permanent";
        }

        if (termType == TermKind.Finite && termValue is > 0 && termUnit == TermUnitKind.Year)
        {
            return termValue.Value == 1 ? "1 year" : $"{termValue.Value} years";
        }

        return "Unknown term";
    }
}
