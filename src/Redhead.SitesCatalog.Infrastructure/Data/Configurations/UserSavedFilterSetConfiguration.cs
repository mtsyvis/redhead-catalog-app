using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class UserSavedFilterSetConfiguration : IEntityTypeConfiguration<UserSavedFilterSet>
{
    public void Configure(EntityTypeBuilder<UserSavedFilterSet> builder)
    {
        builder.ToTable("UserSavedFilterSets", table =>
        {
            table.HasCheckConstraint(
                "CK_UserSavedFilterSets_TableKey",
                $@"""TableKey"" = '{TableViewConstants.SitesTableKey}'");

            table.HasCheckConstraint(
                "CK_UserSavedFilterSets_SchemaVersion",
                $@"""SchemaVersion"" = {SavedFilterSetConstants.SchemaVersion}");

            table.HasCheckConstraint(
                "CK_UserSavedFilterSets_SettingsJsonLength",
                $@"length(""SettingsJson""::text) <= {SavedFilterSetConstants.SettingsJsonMaxLength}");
        });

        builder.HasKey(filterSet => filterSet.Id);

        builder.Property(filterSet => filterSet.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(filterSet => filterSet.TableKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(filterSet => filterSet.Name)
            .IsRequired()
            .HasMaxLength(SavedFilterSetConstants.FilterSetNameMaxLength);

        builder.Property(filterSet => filterSet.NormalizedName)
            .IsRequired()
            .HasMaxLength(SavedFilterSetConstants.FilterSetNameMaxLength);

        builder.Property(filterSet => filterSet.SchemaVersion)
            .IsRequired();

        builder.Property(filterSet => filterSet.SettingsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(filterSet => filterSet.CreatedAtUtc)
            .IsRequired();

        builder.Property(filterSet => filterSet.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(filterSet => new { filterSet.UserId, filterSet.TableKey, filterSet.NormalizedName })
            .IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(filterSet => filterSet.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
