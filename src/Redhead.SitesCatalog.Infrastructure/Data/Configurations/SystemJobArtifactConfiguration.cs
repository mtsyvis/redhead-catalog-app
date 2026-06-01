using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class SystemJobArtifactConfiguration : IEntityTypeConfiguration<SystemJobArtifact>
{
    public void Configure(EntityTypeBuilder<SystemJobArtifact> builder)
    {
        builder.ToTable("SystemJobArtifacts", table =>
        {
            table.HasCheckConstraint(
                "CK_SystemJobArtifacts_FileSizeBytes_NonNegative",
                @"""FileSizeBytes"" >= 0");
        });

        builder.HasKey(artifact => artifact.Id);

        builder.Property(artifact => artifact.FileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(artifact => artifact.FileSizeBytes)
            .IsRequired();

        builder.Property(artifact => artifact.StorageProvider)
            .HasMaxLength(64);

        builder.Property(artifact => artifact.StoragePath)
            .HasMaxLength(1024);

        builder.Property(artifact => artifact.ExternalFileId)
            .HasMaxLength(256);

        builder.Property(artifact => artifact.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(artifact => artifact.SystemJobRunId);
        builder.HasIndex(artifact => artifact.ExternalFileId);
    }
}
