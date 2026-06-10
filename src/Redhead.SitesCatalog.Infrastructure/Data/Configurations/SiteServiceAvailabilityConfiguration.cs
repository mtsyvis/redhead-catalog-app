using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SiteServiceAvailabilityConfiguration : IEntityTypeConfiguration<SiteServiceAvailability>
{
    public void Configure(EntityTypeBuilder<SiteServiceAvailability> builder)
    {
        builder.ToTable("SiteServiceAvailabilities", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SiteServiceAvailabilities_ServiceType_NotMain",
                "\"ServiceType\" <> 0");
        });

        builder.HasKey(availability => availability.Id);

        builder.Property(availability => availability.SiteDomain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);

        builder.Property(availability => availability.ServiceType)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(availability => availability.Status)
            .IsRequired()
            .HasConversion<short>()
            .HasColumnType("smallint");

        builder.Property(availability => availability.CreatedAtUtc)
            .IsRequired();

        builder.Property(availability => availability.UpdatedAtUtc)
            .IsRequired();

        builder.HasOne(availability => availability.Site)
            .WithMany(site => site.ServiceAvailabilities)
            .HasForeignKey(availability => availability.SiteDomain)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(availability => new { availability.SiteDomain, availability.ServiceType })
            .IsUnique();
    }
}
