using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class SiteMetricSnapshotConfiguration : IEntityTypeConfiguration<SiteMetricSnapshot>
{
    public void Configure(EntityTypeBuilder<SiteMetricSnapshot> builder)
    {
        builder.ToTable("SiteMetricSnapshots");
        builder.HasKey(snapshot => snapshot.Id);
        builder.Property(snapshot => snapshot.Domain)
            .IsRequired()
            .HasMaxLength(SiteFieldLimits.DomainMaxLength);
        builder.Property(snapshot => snapshot.SnapshotDate)
            .HasColumnType("date")
            .IsRequired();
        builder.Property(snapshot => snapshot.Source)
            .IsRequired()
            .HasMaxLength(64);
        builder.Property(snapshot => snapshot.FetchedAt).IsRequired();
        builder.HasIndex(snapshot => new { snapshot.Domain, snapshot.SnapshotDate })
            .IsUnique();
        builder.HasOne(snapshot => snapshot.AhrefsSyncRun)
            .WithMany(run => run.Snapshots)
            .HasForeignKey(snapshot => snapshot.AhrefsSyncRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
