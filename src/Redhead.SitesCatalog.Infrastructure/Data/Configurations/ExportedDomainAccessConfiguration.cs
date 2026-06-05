using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class ExportedDomainAccessConfiguration : IEntityTypeConfiguration<ExportedDomainAccess>
{
    public void Configure(EntityTypeBuilder<ExportedDomainAccess> builder)
    {
        builder.ToTable("ExportedDomainAccesses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExportLogId)
            .IsRequired();

        builder.HasIndex(e => e.ExportLogId);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(e => e.Domain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);

        builder.Property(e => e.ExportedAtUtc)
            .IsRequired();

        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.ExportedAtUtc);
        builder.HasIndex(e => e.Domain);
        builder.HasIndex(e => new { e.UserId, e.ExportedAtUtc });
        builder.HasIndex(e => new { e.UserId, e.Domain, e.ExportedAtUtc });
    }
}
