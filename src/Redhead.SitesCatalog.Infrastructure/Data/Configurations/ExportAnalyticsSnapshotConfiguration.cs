using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class ExportAnalyticsSnapshotConfiguration : IEntityTypeConfiguration<ExportAnalyticsSnapshot>
{
    public void Configure(EntityTypeBuilder<ExportAnalyticsSnapshot> builder)
    {
        builder.ToTable("ExportAnalyticsSnapshots");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExportLogId)
            .IsRequired();

        builder.HasIndex(e => e.ExportLogId)
            .IsUnique();

        builder.HasOne(e => e.ExportLog)
            .WithOne(e => e.AnalyticsSnapshot)
            .HasForeignKey<ExportAnalyticsSnapshot>(e => e.ExportLogId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.Property(e => e.SnapshotVersion)
            .IsRequired();

        builder.Property(e => e.FiltersSnapshotJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.SortSnapshotJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(e => e.SearchSnapshotJson)
            .HasColumnType("jsonb");

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired();
    }
}
