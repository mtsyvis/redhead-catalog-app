using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class UserTableCustomViewConfiguration : IEntityTypeConfiguration<UserTableCustomView>
{
    public void Configure(EntityTypeBuilder<UserTableCustomView> builder)
    {
        builder.ToTable("UserTableCustomViews", table =>
        {
            table.HasCheckConstraint(
                "CK_UserTableCustomViews_TableKey",
                $@"""TableKey"" = '{TableViewConstants.SitesTableKey}'");

            table.HasCheckConstraint(
                "CK_UserTableCustomViews_SchemaVersion",
                $@"""SchemaVersion"" = {TableViewConstants.SchemaVersion}");

            table.HasCheckConstraint(
                "CK_UserTableCustomViews_SettingsJsonLength",
                $@"length(""SettingsJson""::text) <= {TableViewConstants.SettingsJsonMaxLength}");
        });

        builder.HasKey(view => view.Id);

        builder.Property(view => view.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(view => view.TableKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(view => view.Name)
            .IsRequired()
            .HasMaxLength(TableViewConstants.CustomViewNameMaxLength);

        builder.Property(view => view.NormalizedName)
            .IsRequired()
            .HasMaxLength(TableViewConstants.CustomViewNameMaxLength);

        builder.Property(view => view.SchemaVersion)
            .IsRequired();

        builder.Property(view => view.SettingsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(view => view.CreatedAtUtc)
            .IsRequired();

        builder.Property(view => view.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(view => new { view.UserId, view.TableKey, view.NormalizedName })
            .IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(view => view.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
