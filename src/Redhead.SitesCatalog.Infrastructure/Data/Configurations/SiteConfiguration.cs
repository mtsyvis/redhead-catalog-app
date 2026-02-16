using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");

        builder.HasKey(s => s.Domain);

        builder.Property(s => s.Domain)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.DR)
            .IsRequired();

        builder.Property(s => s.Traffic)
            .IsRequired();

        builder.Property(s => s.Location)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => s.Location);

        builder.Property(s => s.PriceUsd)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceCasino)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceCrypto)
            .HasPrecision(18, 2);

        builder.Property(s => s.PriceLinkInsert)
            .HasPrecision(18, 2);

        builder.Property(s => s.Niche)
            .HasColumnType("text");

        builder.Property(s => s.Categories)
            .HasColumnType("text");

        builder.Property(s => s.IsQuarantined)
            .IsRequired();

        builder.HasIndex(s => s.IsQuarantined);

        builder.Property(s => s.QuarantineReason)
            .HasMaxLength(1000);

        builder.Property(s => s.QuarantineUpdatedAtUtc);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.Property(s => s.UpdatedAtUtc)
            .IsRequired();

        // Optional: Create indexes for performance
        builder.HasIndex(s => s.DR);
        builder.HasIndex(s => s.Traffic);
        builder.HasIndex(s => s.PriceUsd);
    }
}
