using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Validation;

public sealed class WebmasterOfferWriteValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_ValidRequest_TrimsTextAndRemovesEmptyUnknownPrice()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = DateTime.UtcNow,
            Status = SiteWebmasterOfferStatus.Active,
            CommentText = " comment ",
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Main,
                    AvailabilityStatus = ServiceAvailabilityStatus.Available,
                    WebmasterPriceUsd = 100.25m,
                    WebmasterPriceDetails = " standard "
                },
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Crypto,
                    AvailabilityStatus = ServiceAvailabilityStatus.Unknown
                }
            ]
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("comment", result.NormalizedRequest!.CommentText);
        var price = Assert.Single(result.NormalizedRequest.Prices);
        Assert.Equal(100.25m, price.WebmasterPriceUsd);
        Assert.Equal("standard", price.WebmasterPriceDetails);
    }

    [Fact]
    public void ValidateAndNormalize_MainAvailableWithUnknownPrice_ReturnsFieldError()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = DateTime.UtcNow,
            Status = SiteWebmasterOfferStatus.Active,
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Main,
                    AvailabilityStatus = ServiceAvailabilityStatus.AvailableWithUnknownPrice
                }
            ]
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("prices.0.availabilityStatus", result.FieldErrors.Keys);
    }

    [Fact]
    public void ValidateAndNormalize_MissingMainPrice_IsValid()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = DateTime.UtcNow,
            Status = SiteWebmasterOfferStatus.Active,
            Prices = []
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.NormalizedRequest!.Prices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateAndNormalize_NonPositiveNumericPrice_ReturnsFieldError(decimal amount)
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedUpdatedAtUtc = DateTime.UtcNow,
            Status = SiteWebmasterOfferStatus.Active,
            Prices =
            [
                new UpdateWebmasterOfferPriceRequest
                {
                    PriceType = WebmasterOfferPriceType.Casino,
                    AvailabilityStatus = ServiceAvailabilityStatus.Available,
                    WebmasterPriceUsd = amount
                }
            ]
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("prices.1.webmasterPriceUsd", result.FieldErrors.Keys);
    }
}
