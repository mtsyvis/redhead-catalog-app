using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class AhrefsSyncRunConfiguration : IEntityTypeConfiguration<AhrefsSyncRun>
{
    public void Configure(EntityTypeBuilder<AhrefsSyncRun> builder)
    {
        builder.ToTable("AhrefsSyncRuns", table =>
        {
            table.HasCheckConstraint(
                "CK_AhrefsSyncRuns_Status_ValidValue",
                @"""Status"" IN (1, 2, 3, 4, 5, 6, 7, 8)");
            table.HasCheckConstraint(
                "CK_AhrefsSyncRuns_RunKind_ValidValue",
                @"""RunKind"" IN (1, 2, 3, 4)");
        });
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Status).HasConversion<int>().IsRequired();
        builder.Property(run => run.RunKind).HasConversion<int>().IsRequired();
        builder.Property(run => run.SnapshotMonth).HasColumnType("date").IsRequired();
        builder.Property(run => run.TargetMode).HasMaxLength(32).IsRequired();
        builder.Property(run => run.Protocol).HasMaxLength(32).IsRequired();
        builder.Property(run => run.VolumeMode).HasMaxLength(32).IsRequired();
        builder.Property(run => run.TriggeredByUserId).HasMaxLength(450);
        builder.Property(run => run.ErrorMessage).HasColumnType("text");
        builder.HasIndex(run => new { run.SnapshotMonth, run.Status, run.IsFullCoverage });
        builder.HasIndex(run => run.StartedAt);
        builder.HasMany(run => run.Items)
            .WithOne(item => item.Run)
            .HasForeignKey(item => item.RunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
