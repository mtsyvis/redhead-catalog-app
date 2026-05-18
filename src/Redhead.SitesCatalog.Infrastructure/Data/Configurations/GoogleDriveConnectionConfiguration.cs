using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class GoogleDriveConnectionConfiguration : IEntityTypeConfiguration<GoogleDriveConnection>
{
    public void Configure(EntityTypeBuilder<GoogleDriveConnection> builder)
    {
        builder.ToTable("GoogleDriveConnections");

        builder.HasKey(connection => connection.Id);

        builder.Property(connection => connection.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(connection => connection.GoogleSubjectId)
            .HasMaxLength(256);

        builder.Property(connection => connection.GoogleEmail)
            .HasMaxLength(256);

        builder.Property(connection => connection.RefreshTokenEncrypted)
            .IsRequired();

        builder.Property(connection => connection.GrantedScopes)
            .IsRequired();

        builder.Property(connection => connection.ExportFolderId)
            .HasMaxLength(256);

        builder.Property(connection => connection.ExportFolderName)
            .HasMaxLength(256);

        builder.Property(connection => connection.ConnectedAtUtc)
            .IsRequired();

        builder.Property(connection => connection.UpdatedAtUtc)
            .IsRequired();

        builder.Property(connection => connection.LastError)
            .HasMaxLength(1000);

        builder.HasIndex(connection => connection.UserId)
            .IsUnique()
            .HasFilter(@"""RevokedAtUtc"" IS NULL");

        builder.HasIndex(connection => connection.GoogleEmail);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(connection => connection.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
