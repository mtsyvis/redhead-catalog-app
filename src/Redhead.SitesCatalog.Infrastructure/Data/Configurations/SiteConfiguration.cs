using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;
using Redhead.SitesCatalog.Domain.Enums;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Sites_PriceCasino_StatusConsistency",
                "(\"PriceCasinoStatus\" = 1 AND \"PriceCasino\" IS NOT NULL AND \"PriceCasino\" >= 0) OR (\"PriceCasinoStatus\" IN (0, 2) AND \"PriceCasino\" IS NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_PriceCrypto_StatusConsistency",
                "(\"PriceCryptoStatus\" = 1 AND \"PriceCrypto\" IS NOT NULL AND \"PriceCrypto\" >= 0) OR (\"PriceCryptoStatus\" IN (0, 2) AND \"PriceCrypto\" IS NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_PriceLinkInsert_StatusConsistency",
                "(\"PriceLinkInsertStatus\" = 1 AND \"PriceLinkInsert\" IS NOT NULL AND \"PriceLinkInsert\" >= 0) OR (\"PriceLinkInsertStatus\" IN (0, 2) AND \"PriceLinkInsert\" IS NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_PriceLinkInsertCasino_StatusConsistency",
                "(\"PriceLinkInsertCasinoStatus\" = 1 AND \"PriceLinkInsertCasino\" IS NOT NULL AND \"PriceLinkInsertCasino\" >= 0) OR (\"PriceLinkInsertCasinoStatus\" IN (0, 2) AND \"PriceLinkInsertCasino\" IS NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_PriceDating_StatusConsistency",
                "(\"PriceDatingStatus\" = 1 AND \"PriceDating\" IS NOT NULL AND \"PriceDating\" >= 0) OR (\"PriceDatingStatus\" IN (0, 2) AND \"PriceDating\" IS NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_NumberDFLinks_PositiveOrNull",
                "\"NumberDFLinks\" IS NULL OR \"NumberDFLinks\" > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Sites_Term_Consistency",
                "(\"TermType\" IS NULL AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 1 AND \"TermValue\" IS NULL AND \"TermUnit\" IS NULL) OR (\"TermType\" = 2 AND \"TermValue\" IS NOT NULL AND \"TermValue\" > 0 AND \"TermUnit\" = 1)");
        });

        builder.HasKey(s => s.Domain);

        builder.Property(s => s.Domain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);

        builder.Property(s => s.DR)
            .IsRequired();

        builder.Property(s => s.Traffic)
            .IsRequired();

        builder.Property(s => s.Location)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.LocationMaxLength);

        builder.HasIndex(s => s.Location);

        builder.Property(s => s.PriceUsd)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceCasino)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceCasinoStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(ServiceAvailabilityStatus.Unknown);

        builder.Property(s => s.PriceCrypto)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceCryptoStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(ServiceAvailabilityStatus.Unknown);

        builder.Property(s => s.PriceLinkInsert)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceLinkInsertStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(ServiceAvailabilityStatus.Unknown);

        builder.Property(s => s.PriceLinkInsertCasino)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceLinkInsertCasinoStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(ServiceAvailabilityStatus.Unknown);

        builder.Property(s => s.PriceDating)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceDatingStatus)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(ServiceAvailabilityStatus.Unknown);

        builder.Property(s => s.NumberDFLinks);

        builder.Property(s => s.TermType)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(s => s.TermValue);

        builder.Property(s => s.TermUnit)
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(s => s.Niche)
            .HasColumnType("text");

        builder.Property(s => s.Categories)
            .HasColumnType("text");

        builder.Property(s => s.LinkType)
            .HasMaxLength(SiteFieldLimits.LinkTypeMaxLength);

        builder.Property(s => s.SponsoredTag)
            .HasMaxLength(SiteFieldLimits.SponsoredTagMaxLength);

        builder.Property(s => s.IsQuarantined)
            .IsRequired();

        builder.HasIndex(s => s.IsQuarantined);

        builder.Property(s => s.QuarantineReason)
            .HasMaxLength(SiteFieldLimits.QuarantineReasonMaxLength);

        builder.Property(s => s.QuarantineUpdatedAtUtc);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        builder.Property(s => s.LastPublishedDate)
            .HasColumnType("date");

        builder.Property(s => s.LastPublishedDateIsMonthOnly)
            .IsRequired()
            .HasDefaultValue(false);

        // Optional: Create indexes for performance
        builder.HasIndex(s => s.DR);
        builder.HasIndex(s => s.Traffic);
        builder.HasIndex(s => s.PriceUsd);
        builder.HasIndex(s => new { s.LastPublishedDate, s.LastPublishedDateIsMonthOnly, s.Domain });
    }
}
