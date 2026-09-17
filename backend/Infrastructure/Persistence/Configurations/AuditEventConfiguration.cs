using KeplerTalento.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AUD_Events", table =>
            // Null on both columns is the historic, pre-KTL-19 row; otherwise the kind decides
            // whether a user id is present. A system row naming a user is as wrong as a user row
            // naming nobody.
            table.HasCheckConstraint(
                "CK_AUD_Events_Actor",
                "(\"ActorKind\" IS NULL AND \"ActorUserId\" IS NULL)"
                + " OR (\"ActorKind\" = 'system' AND \"ActorUserId\" IS NULL)"
                + " OR (\"ActorKind\" = 'user' AND \"ActorUserId\" IS NOT NULL)"));
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.EventType).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.SubjectId).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.OutcomeCode).HasMaxLength(100);
        builder.Property(audit => audit.ActorKind).HasMaxLength(16);
        builder.Property(audit => audit.ActorUserId);
        builder.Ignore(audit => audit.Actor);

        builder.HasIndex(audit => audit.CorrelationId);
        // Filter indexes for the audit read surface (KTL-19 design D7). Every listing orders by
        // CreatedAtUtc descending, so each filter column leads a composite ending in it.
        builder.HasIndex(audit => audit.CreatedAtUtc)
            .IsDescending(true)
            .HasDatabaseName("IX_AUD_Events_CreatedAtUtc");
        builder.HasIndex(audit => new { audit.EventType, audit.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AUD_Events_EventType_CreatedAtUtc");
        builder.HasIndex(audit => new { audit.ActorUserId, audit.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AUD_Events_ActorUserId_CreatedAtUtc");
        builder.HasIndex(audit => new { audit.SubjectId, audit.CreatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_AUD_Events_SubjectId_CreatedAtUtc");
    }
}
