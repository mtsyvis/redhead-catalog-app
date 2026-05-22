using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public class UserTablePreferenceConfiguration : IEntityTypeConfiguration<UserTablePreference>
{
    public void Configure(EntityTypeBuilder<UserTablePreference> builder)
    {
        builder.ToTable("UserTablePreferences", table =>
        {
            table.HasCheckConstraint(
                "CK_UserTablePreferences_TableKey",
                $@"""TableKey"" = '{TableViewConstants.SitesTableKey}'");

            table.HasCheckConstraint(
                "CK_UserTablePreferences_ActiveViewType",
                $@"""ActiveViewType"" IN ('{TableViewConstants.SystemViewType}', '{TableViewConstants.CustomViewType}')");
        });

        builder.HasKey(preference => preference.Id);

        builder.Property(preference => preference.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(preference => preference.TableKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(preference => preference.ActiveViewType)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(preference => preference.ActiveViewKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(preference => preference.CreatedAtUtc)
            .IsRequired();

        builder.Property(preference => preference.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(preference => new { preference.UserId, preference.TableKey })
            .IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(preference => preference.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
