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

        builder.Property(e => e.RequestedRowsCount)
            .IsRequired();

        builder.Property(e => e.ExportedRowsCount)
            .IsRequired();

        builder.Property(e => e.WasTruncated)
            .IsRequired();

        builder.Property(e => e.ExportLimitRows)
            .IsRequired(false);

        builder.Property(e => e.DailyUniqueExportedDomainsLimit)
            .IsRequired(false);

        builder.Property(e => e.WeeklyUniqueExportedDomainsLimit)
            .IsRequired(false);

        builder.Property(e => e.DailyExportOperationsLimit)
            .IsRequired(false);

        builder.Property(e => e.WeeklyExportOperationsLimit)
            .IsRequired(false);

        builder.Property(e => e.Destination)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.Destination);

        builder.Property(e => e.ExportMode)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(e => e.ExportMode);

        builder.Property(e => e.BlockedReason)
            .HasMaxLength(100);

        builder.HasIndex(e => new { e.UserId, e.TimestampUtc, e.BlockedReason });

        builder.HasMany(e => e.ExportedDomainAccesses)
            .WithOne(e => e.ExportLog)
            .HasForeignKey(e => e.ExportLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
