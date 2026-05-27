using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class LocationGroupItemConfiguration : IEntityTypeConfiguration<LocationGroupItem>
{
    public void Configure(EntityTypeBuilder<LocationGroupItem> builder)
    {
        builder.ToTable("LocationGroupItems");

        builder.HasKey(item => new { item.GroupKey, item.LocationKey });

        builder.Property(item => item.GroupKey)
            .HasMaxLength(LocationConstants.LocationGroupKeyMaxLength);

        builder.Property(item => item.LocationKey)
            .HasMaxLength(LocationConstants.LocationKeyMaxLength);

        builder.HasOne(item => item.Group)
            .WithMany(group => group.Items)
            .HasForeignKey(item => item.GroupKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.Location)
            .WithMany(location => location.GroupItems)
            .HasForeignKey(item => item.LocationKey)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => item.LocationKey);
    }
}
