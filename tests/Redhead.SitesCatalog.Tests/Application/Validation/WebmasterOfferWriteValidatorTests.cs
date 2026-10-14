using Redhead.SitesCatalog.Application.Models.WebmasterOffers;
using Redhead.SitesCatalog.Application.Validation;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Application.Validation;

public sealed class WebmasterOfferWriteValidatorTests
{
    [Fact]
    public void ValidateAndNormalize_ZeroVersions_DefersConcurrencyCheckToDatabase()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(0U, result.NormalizedRequest!.ExpectedVersion);
        Assert.Equal(0U, result.NormalizedRequest.ExpectedWebmasterVersion);
    }

    [Fact]
    public void ValidateAndNormalize_ValidRequest_TrimsTextAndRemovesEmptyUnknownPrice()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 1,
            ContactRawText = " contact@example.com ",
            PrimaryEmail = " CONTACT@EXAMPLE.COM ",
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
        Assert.Equal("contact@example.com", result.NormalizedRequest!.ContactRawText);
        Assert.Equal("contact@example.com", result.NormalizedRequest.PrimaryEmail);
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
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 1,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
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
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 1,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
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
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 1,
            ContactRawText = "contact@example.com",
            PrimaryEmail = "contact@example.com",
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

    [Theory]
    [InlineData("", null, "contactRawText")]
    public void ValidateAndNormalize_InvalidContactFields_ReturnsFieldError(
        string contactRawText,
        string? primaryEmail,
        string expectedField)
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ExpectedVersion = 1,
            ExpectedWebmasterVersion = 1,
            ContactRawText = contactRawText,
            PrimaryEmail = primaryEmail,
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(expectedField, result.FieldErrors.Keys);
    }

    [Theory]
    [InlineData("  WHL - по вопросом публикации к Лене  ", "WHL - по вопросом публикации к Лене")]
    [InlineData("  CONTACT@EXAMPLE.COM  ", "contact@example.com")]
    public void ValidateAndNormalize_ExplicitPrimaryContact_IsAccepted(string primary, string expected)
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest { ContactRawText = "first@example.com second@example.com", PrimaryEmail = primary, Status = SiteWebmasterOfferStatus.Active };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(expected, result.NormalizedRequest!.PrimaryEmail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void ValidateAndNormalize_EmptyPrimaryEmail_ReturnsRequiredErrorWithoutFallback(string? primary)
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest
        {
            ContactRawText = "first@example.com second@example.com (reply)",
            PrimaryEmail = primary,
            Status = SiteWebmasterOfferStatus.Active
        };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Null(result.NormalizedRequest);
        Assert.Equal("Primary email is required.", Assert.Single(result.FieldErrors["primaryEmail"]));
        Assert.Equal(primary, request.PrimaryEmail);
    }

    [Fact]
    public void ValidateAndNormalize_LongPrimaryContact_IsBounded()
    {
        // Arrange
        var request = new UpdateWebmasterOfferRequest { ContactRawText = "contact@example.com", PrimaryEmail = new string('x', 20_001), Status = SiteWebmasterOfferStatus.Active };

        // Act
        var result = WebmasterOfferWriteValidator.ValidateAndNormalize(request);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("primaryEmail", result.FieldErrors.Keys);
    }
}
