using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class LocationGroupConfiguration : IEntityTypeConfiguration<LocationGroup>
{
    public void Configure(EntityTypeBuilder<LocationGroup> builder)
    {
        builder.ToTable("LocationGroups");

        builder.HasKey(group => group.Key);

        builder.Property(group => group.Key)
            .HasMaxLength(LocationConstants.LocationGroupKeyMaxLength)
            .ValueGeneratedNever();

        builder.Property(group => group.DisplayName)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.LocationMaxLength);

        builder.Property(group => group.Kind)
            .IsRequired()
            .HasMaxLength(LocationConstants.LocationGroupKindMaxLength);

        builder.Property(group => group.SortOrder)
            .IsRequired();

        builder.HasIndex(group => new { group.Kind, group.SortOrder });
    }
}
