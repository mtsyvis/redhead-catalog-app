using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Models;

public class SiteWriteValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_NullInput_ReturnsBodyRequiredError()
    {
        var result = SiteWriteValidator.ValidateAndNormalize(null);

        Assert.False(result.IsValid);
        Assert.True(result.FieldErrors.ContainsKey(string.Empty));
    }

    [Fact]
    public void ValidateAndNormalize_MissingDr_ReturnsError()
    {
        var request = BuildValidRequest();
        request.DR = null;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "dr");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void ValidateAndNormalize_DrOutOfRange_ReturnsError(double dr)
    {
        var request = BuildValidRequest();
        request.DR = dr;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "dr");
    }

    [Fact]
    public void ValidateAndNormalize_MissingTraffic_ReturnsError()
    {
        var request = BuildValidRequest();
        request.Traffic = null;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "traffic");
    }

    [Fact]
    public void ValidateAndNormalize_NegativeTraffic_ReturnsError()
    {
        var request = BuildValidRequest();
        request.Traffic = -1;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "traffic");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAndNormalize_EmptyLocation_ReturnsError(string? location)
    {
        var request = BuildValidRequest();
        request.Location = location;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "location");
    }

    [Fact]
    public void ValidateAndNormalize_LocationTooLong_ReturnsError()
    {
        var request = BuildValidRequest();
        request.Location = new string('a', SiteFieldLimits.LocationMaxLength + 1);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "location");
    }

    [Fact]
    public void ValidateAndNormalize_MissingPriceUsd_ReturnsError()
    {
        var request = BuildValidRequest();
        request.PriceUsd = null;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "priceUsd");
    }

    [Fact]
    public void ValidateAndNormalize_NegativePriceUsd_ReturnsError()
    {
        var request = BuildValidRequest();
        request.PriceUsd = -1;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "priceUsd");
    }

    [Theory]
    [InlineData("priceCasino", "PriceCasino")]
    [InlineData("priceCrypto", "PriceCrypto")]
    [InlineData("priceLinkInsert", "PriceLinkInsert")]
    public void ValidateAndNormalize_AvailableStatusWithoutPrice_ReturnsError(string expectedField, string service)
    {
        var request = BuildValidRequest();
        SetOptionalService(request, service, null, ServiceAvailabilityStatus.Available);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, expectedField);
    }

    [Theory]
    [InlineData("priceCasino", "PriceCasino")]
    [InlineData("priceCrypto", "PriceCrypto")]
    [InlineData("priceLinkInsert", "PriceLinkInsert")]
    public void ValidateAndNormalize_AvailableStatusWithNegativePrice_ReturnsError(string expectedField, string service)
    {
        var request = BuildValidRequest();
        SetOptionalService(request, service, -1m, ServiceAvailabilityStatus.Available);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, expectedField);
    }

    [Theory]
    [InlineData("priceCasino", "PriceCasino")]
    [InlineData("priceCrypto", "PriceCrypto")]
    [InlineData("priceLinkInsert", "PriceLinkInsert")]
    public void ValidateAndNormalize_NonAvailableStatusWithPrice_ReturnsError(string expectedField, string service)
    {
        var request = BuildValidRequest();
        SetOptionalService(request, service, 5m, ServiceAvailabilityStatus.NotAvailable);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, expectedField);
    }

    [Fact]
    public void ValidateAndNormalize_LinkTypeTooLong_ReturnsError()
    {
        var request = BuildValidRequest();
        request.LinkType = new string('x', SiteFieldLimits.LinkTypeMaxLength + 1);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "linkType");
    }

    [Fact]
    public void ValidateAndNormalize_SponsoredTagTooLong_ReturnsError()
    {
        var request = BuildValidRequest();
        request.SponsoredTag = new string('x', SiteFieldLimits.SponsoredTagMaxLength + 1);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "sponsoredTag");
    }

    [Fact]
    public void ValidateAndNormalize_QuarantineReasonTooLong_ReturnsError()
    {
        var request = BuildValidRequest();
        request.IsQuarantined = true;
        request.QuarantineReason = new string('x', SiteFieldLimits.QuarantineReasonMaxLength + 1);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        AssertError(result, "quarantineReason");
    }

    [Fact]
    public void ValidateAndNormalize_CollectsMultipleErrorsInOnePass()
    {
        var request = BuildValidRequest();
        request.DR = 101;
        request.Traffic = -1;
        request.Location = "   ";
        request.PriceUsd = -1;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        Assert.False(result.IsValid);
        Assert.Contains("dr", result.FieldErrors.Keys);
        Assert.Contains("traffic", result.FieldErrors.Keys);
        Assert.Contains("location", result.FieldErrors.Keys);
        Assert.Contains("priceUsd", result.FieldErrors.Keys);
    }

    [Fact]
    public void ValidateAndNormalize_ValidInput_ReturnsNormalizedRequest()
    {
        var request = BuildValidRequest();
        request.Location = "  US  ";
        request.Niche = "  iGaming ";
        request.Categories = "  review, blog ";
        request.LinkType = "  homepage ";
        request.SponsoredTag = "  yes ";
        request.IsQuarantined = true;
        request.QuarantineReason = "  pending check ";
        request.PriceCasino = 20m;
        request.PriceCasinoStatus = ServiceAvailabilityStatus.Available;

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        Assert.True(result.IsValid);
        var normalized = Assert.IsType<UpdateSiteRequest>(result.NormalizedRequest);
        Assert.Equal("US", normalized.Location);
        Assert.Equal("iGaming", normalized.Niche);
        Assert.Equal("review, blog", normalized.Categories);
        Assert.Equal("homepage", normalized.LinkType);
        Assert.Equal("yes", normalized.SponsoredTag);
        Assert.Equal("pending check", normalized.QuarantineReason);
        Assert.Equal(20m, normalized.PriceCasino);
        Assert.Equal(ServiceAvailabilityStatus.Available, normalized.PriceCasinoStatus);
    }

    [Fact]
    public void ValidateAndNormalize_NotQuarantined_ClearsQuarantineReason()
    {
        var request = BuildValidRequest();
        request.IsQuarantined = false;
        request.QuarantineReason = "should be removed";

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        Assert.True(result.IsValid);
        var normalized = Assert.IsType<UpdateSiteRequest>(result.NormalizedRequest);
        Assert.Null(normalized.QuarantineReason);
    }

    [Theory]
    [InlineData("PriceCasino", ServiceAvailabilityStatus.Unknown)]
    [InlineData("PriceCrypto", ServiceAvailabilityStatus.NotAvailable)]
    [InlineData("PriceLinkInsert", ServiceAvailabilityStatus.Unknown)]
    public void ValidateAndNormalize_NonAvailableStatuses_ForceNormalizedPriceToNull(string service, ServiceAvailabilityStatus status)
    {
        var request = BuildValidRequest();
        SetOptionalService(request, service, null, status);

        var result = SiteWriteValidator.ValidateAndNormalize(request);

        Assert.True(result.IsValid);
        var normalized = Assert.IsType<UpdateSiteRequest>(result.NormalizedRequest);

        switch (service)
        {
            case "PriceCasino":
                Assert.Null(normalized.PriceCasino);
                break;
            case "PriceCrypto":
                Assert.Null(normalized.PriceCrypto);
                break;
            default:
                Assert.Null(normalized.PriceLinkInsert);
                break;
        }
    }

    private static void SetOptionalService(
        SiteWriteInput request,
        string service,
        decimal? price,
        ServiceAvailabilityStatus status)
    {
        switch (service)
        {
            case "PriceCasino":
                request.PriceCasino = price;
                request.PriceCasinoStatus = status;
                break;
            case "PriceCrypto":
                request.PriceCrypto = price;
                request.PriceCryptoStatus = status;
                break;
            default:
                request.PriceLinkInsert = price;
                request.PriceLinkInsertStatus = status;
                break;
        }
    }

    private static void AssertError(SiteWriteValidationResult result, string fieldName)
    {
        Assert.False(result.IsValid);
        Assert.Contains(fieldName, result.FieldErrors.Keys);
    }

    private static SiteWriteInput BuildValidRequest()
    {
        return new SiteWriteInput
        {
            DR = 50,
            Traffic = 1000,
            Location = "US",
            PriceUsd = 10m,
            PriceCasino = null,
            PriceCasinoStatus = ServiceAvailabilityStatus.Unknown,
            PriceCrypto = null,
            PriceCryptoStatus = ServiceAvailabilityStatus.Unknown,
            PriceLinkInsert = null,
            PriceLinkInsertStatus = ServiceAvailabilityStatus.Unknown,
            Niche = null,
            Categories = null,
            LinkType = null,
            SponsoredTag = null,
            IsQuarantined = false,
            QuarantineReason = null
        };
    }
}
