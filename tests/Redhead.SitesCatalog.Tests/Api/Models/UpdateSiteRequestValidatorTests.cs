using Redhead.SitesCatalog.Api.Models.Sites;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Tests.Api.Models;

public class UpdateSiteRequestValidatorTests
{
    [Fact]
    public void Validate_AvailableWithNullPrice_ReturnsError()
    {
        var request = BuildValidRequest();
        request.PriceCasino = null;
        request.PriceCasinoStatus = ServiceAvailabilityStatus.Available;

        var errors = UpdateSiteRequestValidator.Validate(request);

        Assert.NotNull(errors);
        Assert.Contains("priceCasino", errors!.Keys);
    }

    [Fact]
    public void Validate_UnknownWithPrice_ReturnsError()
    {
        var request = BuildValidRequest();
        request.PriceCrypto = 12m;
        request.PriceCryptoStatus = ServiceAvailabilityStatus.Unknown;

        var errors = UpdateSiteRequestValidator.Validate(request);

        Assert.NotNull(errors);
        Assert.Contains("priceCrypto", errors!.Keys);
    }

    [Fact]
    public void Validate_NotAvailableWithPrice_ReturnsError()
    {
        var request = BuildValidRequest();
        request.PriceLinkInsert = 10m;
        request.PriceLinkInsertStatus = ServiceAvailabilityStatus.NotAvailable;

        var errors = UpdateSiteRequestValidator.Validate(request);

        Assert.NotNull(errors);
        Assert.Contains("priceLinkInsert", errors!.Keys);
    }

    [Fact]
    public void Validate_AvailableWithPrice_IsValid()
    {
        var request = BuildValidRequest();
        request.PriceCasino = 20m;
        request.PriceCasinoStatus = ServiceAvailabilityStatus.Available;

        var errors = UpdateSiteRequestValidator.Validate(request);

        Assert.Null(errors);
    }

    private static UpdateSiteRequest BuildValidRequest()
    {
        return new UpdateSiteRequest
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
            IsQuarantined = false
        };
    }
}
