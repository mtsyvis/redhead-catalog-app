using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SystemJobRunConfiguration : IEntityTypeConfiguration<SystemJobRun>
{
    public void Configure(EntityTypeBuilder<SystemJobRun> builder)
    {
        builder.ToTable("SystemJobRuns", table =>
        {
            table.HasCheckConstraint(
                "CK_SystemJobRuns_Status_ValidValue",
                @"""Status"" IN (1, 2, 3)");
        });

        builder.HasKey(run => run.Id);

        builder.Property(run => run.JobName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(run => run.PeriodKey)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(run => run.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(run => run.StartedAtUtc)
            .IsRequired();

        builder.Property(run => run.ErrorMessage)
            .HasColumnType("text");

        builder.Property(run => run.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(run => run.Status);
        builder.HasIndex(run => run.StartedAtUtc);

        builder.HasIndex(run => new { run.JobName, run.PeriodKey })
            .IsUnique()
            .HasFilter(@"""Status"" = 2")
            .HasDatabaseName("UX_SystemJobRuns_JobName_PeriodKey_Succeeded");

        builder.HasMany(run => run.Artifacts)
            .WithOne(artifact => artifact.SystemJobRun)
            .HasForeignKey(artifact => artifact.SystemJobRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
