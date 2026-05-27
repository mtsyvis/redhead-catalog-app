using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class CanonicalLocationConfiguration : IEntityTypeConfiguration<CanonicalLocation>
{
    public void Configure(EntityTypeBuilder<CanonicalLocation> builder)
    {
        builder.ToTable("CanonicalLocations");

        builder.HasKey(location => location.Key);

        builder.Property(location => location.Key)
            .HasMaxLength(LocationConstants.LocationKeyMaxLength)
            .ValueGeneratedNever();

        builder.Property(location => location.DisplayName)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.LocationMaxLength);

        builder.Property(location => location.SortOrder)
            .IsRequired();

        builder.Property(location => location.IsActive)
            .IsRequired();

        builder.HasIndex(location => location.DisplayName);
        builder.HasIndex(location => location.SortOrder);
    }
}
