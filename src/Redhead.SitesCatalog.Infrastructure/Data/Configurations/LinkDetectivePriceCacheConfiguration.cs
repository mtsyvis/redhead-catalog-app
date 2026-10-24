using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class LinkDetectivePriceCacheConfiguration : IEntityTypeConfiguration<LinkDetectivePriceCache>
{
    public void Configure(EntityTypeBuilder<LinkDetectivePriceCache> builder)
    {
        builder.HasKey(row => row.SiteDomain);
        builder.Property(row => row.SiteDomain).HasMaxLength(253);
        builder.Property(row => row.MinimumUsd).HasPrecision(18, 2);
        builder.Property(row => row.AverageUsd).HasPrecision(18, 2);
        builder.Property(row => row.MedianUsd).HasPrecision(18, 2);
        builder.Property(row => row.LastError).HasMaxLength(500);
        builder.HasOne<Site>().WithMany().HasForeignKey(row => row.SiteDomain).OnDelete(DeleteBehavior.Cascade);
    }
}
