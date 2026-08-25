using KeplerTalento.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class OperationConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.ToTable("OPS_Operations", table => table.HasCheckConstraint("CK_OPS_Operations_Attempts", "\"AttemptCount\" >= 0 AND \"MaxAttempts\" > 0"));
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Type).HasMaxLength(80).IsRequired();
        builder.Property(operation => operation.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(operation => operation.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(operation => operation.IdempotencyKey).HasMaxLength(160).IsRequired();
        builder.HasIndex(operation => operation.IdempotencyKey).IsUnique();
        builder.Property(operation => operation.Owner).HasMaxLength(100);
        builder.Property(operation => operation.OutcomeCode).HasMaxLength(100);
        builder.Property(operation => operation.Version).IsRowVersion();
        builder.HasIndex(operation => new { operation.Status, operation.LeaseExpiresAtUtc });
    }
}
