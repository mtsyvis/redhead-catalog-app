using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SitePriceOptionConfiguration : IEntityTypeConfiguration<SitePriceOption>
{
    public void Configure(EntityTypeBuilder<SitePriceOption> builder)
    {
        builder.ToTable("SitePriceOptions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SitePriceOptions_AmountUsd_Positive",
                "\"AmountUsd\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_SitePriceOptions_Term_Consistency",
                "(\"TermKey\" = 'unknown' AND \"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR " +
                "(\"TermKey\" = 'permanent' AND \"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR " +
                "(\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1 AND \"TermKey\" = ('finite:' || \"TermValue\"::text || ':year'))");
        });

        builder.HasKey(priceOption => priceOption.Id);

        builder.Property(priceOption => priceOption.SiteDomain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);

        builder.Property(priceOption => priceOption.PriceType)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(priceOption => priceOption.TermKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(priceOption => priceOption.TermType)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(priceOption => priceOption.TermValue);

        builder.Property(priceOption => priceOption.TermUnit)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(priceOption => priceOption.AmountUsd)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(priceOption => priceOption.CreatedAtUtc)
            .IsRequired();

        builder.Property(priceOption => priceOption.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(priceOption => priceOption.Site)
            .WithMany(site => site.PriceOptions)
            .HasForeignKey(priceOption => priceOption.SiteDomain)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(priceOption => new { priceOption.SiteDomain, priceOption.PriceType, priceOption.TermKey })
            .IsUnique();

        builder.HasIndex(priceOption => new { priceOption.PriceType, priceOption.TermKey, priceOption.AmountUsd });
        builder.HasIndex(priceOption => new { priceOption.SiteDomain, priceOption.PriceType });
    }
}
