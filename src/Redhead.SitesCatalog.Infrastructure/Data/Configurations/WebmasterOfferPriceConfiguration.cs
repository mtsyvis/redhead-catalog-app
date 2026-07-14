using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class WebmasterOfferPriceConfiguration : IEntityTypeConfiguration<WebmasterOfferPrice>
{
    public void Configure(EntityTypeBuilder<WebmasterOfferPrice> builder)
    {
        builder.ToTable("WebmasterOfferPrices", table =>
        {
            table.HasCheckConstraint(
                "CK_WebmasterOfferPrices_WebmasterPriceUsd_PositiveOrNull",
                "\"WebmasterPriceUsd\" IS NULL OR \"WebmasterPriceUsd\" > 0");
            table.HasCheckConstraint(
                "CK_WebmasterOfferPrices_AvailabilityStatus_Consistency",
                "(\"AvailabilityStatus\" = 1 AND \"WebmasterPriceUsd\" IS NOT NULL AND \"WebmasterPriceUsd\" > 0) OR " +
                "(\"AvailabilityStatus\" IN (0, 2, 3) AND \"WebmasterPriceUsd\" IS NULL)");
            table.HasCheckConstraint(
                "CK_WebmasterOfferPrices_Term_Consistency",
                "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR " +
                "(\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
        });

        builder.HasKey(price => price.Id);

        builder.Property(price => price.PriceType)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(price => price.AvailabilityStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(price => price.WebmasterPriceUsd)
            .HasPrecision(18, 2);

        builder.Property(price => price.WebmasterPriceDetails)
            .HasColumnType("text");

        builder.Property(price => price.TermType)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(price => price.TermUnit)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(price => price.CreatedAtUtc)
            .IsRequired();

        builder.Property(price => price.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(price => price.SiteWebmasterOffer)
            .WithMany(offer => offer.Prices)
            .HasForeignKey(price => price.SiteWebmasterOfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(price => new { price.SiteWebmasterOfferId, price.PriceType });
        builder.HasIndex(price => price.PriceType);
    }
}
