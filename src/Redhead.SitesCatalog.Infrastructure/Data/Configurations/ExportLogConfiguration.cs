using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class ExportLogConfiguration : IEntityTypeConfiguration<ExportLog>
{
    public void Configure(EntityTypeBuilder<ExportLog> builder)
    {
        builder.ToTable("ExportLogs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(e => e.UserEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(e => e.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.TimestampUtc)
            .IsRequired();

        builder.HasIndex(e => e.TimestampUtc);

        builder.Property(e => e.RowsReturned)
            .IsRequired();

        builder.Property(e => e.FilterSummaryJson)
            .HasColumnType("jsonb");
    }
}
