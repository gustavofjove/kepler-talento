using KeplerTalento.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AUD_Events");
        builder.HasKey(audit => audit.Id);
        builder.Property(audit => audit.EventType).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.SubjectId).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(audit => audit.OutcomeCode).HasMaxLength(100);
        builder.HasIndex(audit => audit.CorrelationId);
    }
}
