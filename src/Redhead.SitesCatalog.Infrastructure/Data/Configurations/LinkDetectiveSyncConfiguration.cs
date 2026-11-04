using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class LinkDetectiveSyncRunConfiguration : IEntityTypeConfiguration<LinkDetectiveSyncRun>
{
    public void Configure(EntityTypeBuilder<LinkDetectiveSyncRun> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(row => row.WaitingReason).HasMaxLength(500);
        builder.HasIndex(row => row.StartedAtUtc);
        builder.HasIndex(row => row.Status);
    }
}

public sealed class LinkDetectiveSyncRunItemConfiguration : IEntityTypeConfiguration<LinkDetectiveSyncRunItem>
{
    public void Configure(EntityTypeBuilder<LinkDetectiveSyncRunItem> builder)
    {
        builder.HasKey(row => new { row.RunId, row.Domain });
        builder.Property(row => row.Domain).HasMaxLength(253);
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(row => row.Error).HasMaxLength(500);
        builder.HasIndex(row => new { row.RunId, row.Status, row.NextAttemptAtUtc });
        builder.HasOne<LinkDetectiveSyncRun>().WithMany().HasForeignKey(row => row.RunId).OnDelete(DeleteBehavior.Cascade);
        // Audit domains outlive catalog deletion; no foreign key to Sites.
    }
}

public sealed class LinkDetectiveSyncStateConfiguration : IEntityTypeConfiguration<LinkDetectiveSyncState>
{
    public void Configure(EntityTypeBuilder<LinkDetectiveSyncState> builder)
    {
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();
        builder.Property(row => row.QuotaPeriod).HasMaxLength(32);
    }
}
