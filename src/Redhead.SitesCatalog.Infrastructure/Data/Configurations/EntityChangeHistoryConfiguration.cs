using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Infrastructure.Data.Configurations;

public sealed class EntityChangeHistoryConfiguration : IEntityTypeConfiguration<EntityChangeHistory>
{
    public void Configure(EntityTypeBuilder<EntityChangeHistory> builder)
    {
        builder.ToTable("EntityChangeHistories");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.EntityType)
            .IsRequired()
            .HasMaxLength(EntityChangeHistoryConstants.EntityTypeMaxLength);

        builder.Property(history => history.EntityId)
            .IsRequired()
            .HasMaxLength(EntityChangeHistoryConstants.EntityIdMaxLength);

        builder.Property(history => history.Action)
            .IsRequired()
            .HasMaxLength(EntityChangeHistoryConstants.ActionMaxLength);

        builder.Property(history => history.Source)
            .IsRequired()
            .HasMaxLength(EntityChangeHistoryConstants.SourceMaxLength);

        builder.Property(history => history.ChangedBy)
            .IsRequired()
            .HasMaxLength(EntityChangeHistoryConstants.ChangedByMaxLength);

        builder.Property(history => history.ChangedAtUtc)
            .IsRequired();

        builder.Property(history => history.ChangesJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.HasIndex(history => new
        {
            history.EntityType,
            history.EntityId,
            history.ChangedAtUtc
        });
    }
}
