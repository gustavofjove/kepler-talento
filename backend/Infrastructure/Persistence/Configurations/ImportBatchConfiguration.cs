using KeplerTalento.Domain.Identity;
using KeplerTalento.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Import batches. <c>ADM_</c> because a batch is an administrative record of work an operator
/// did, not candidate data: it holds counts, codes and a digest. The original filename is the one
/// caller-supplied text on the row, and it never reaches a log.
/// </summary>
public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public const string Table = "ADM_ImportBatches";

    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_State",
                "\"State\" IN (" + string.Join(", ", ImportBatchStates.All.Select(state => $"'{state}'")) + ")");
            table.HasCheckConstraint($"CK_{Table}_SizeBytes", "\"SizeBytes\" > 0");
            table.HasCheckConstraint($"CK_{Table}_Sha256", "\"Sha256\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                $"CK_{Table}_Counts",
                "\"LoadedRows\" >= 0 AND \"RejectedRows\" >= 0 AND \"SkippedRows\" >= 0 AND \"OperationAttempt\" >= 0 "
                + "AND (\"RowCount\" IS NULL AND \"LoadedRows\" + \"RejectedRows\" + \"SkippedRows\" = 0 "
                + "OR \"RowCount\" >= 0 AND \"LoadedRows\" + \"RejectedRows\" + \"SkippedRows\" = \"RowCount\")");
            table.HasCheckConstraint(
                $"CK_{Table}_Timestamps",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            // A purged file is only ever a closed batch's file, and an expired batch always says
            // when its file went.
            table.HasCheckConstraint(
                $"CK_{Table}_Purge",
                "(\"State\" <> 'expired' OR \"FilePurgedAtUtc\" IS NOT NULL) "
                + "AND (\"FilePurgedAtUtc\" IS NULL OR \"State\" IN ('expired', 'infected', 'unscannable'))");
        });

        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.State).HasMaxLength(20).IsRequired();
        builder.Property(batch => batch.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(batch => batch.OriginalFileName).HasMaxLength(ImportBatch.MaximumFileNameLength).IsRequired();
        builder.Property(batch => batch.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(batch => batch.ActorExternalKey).HasMaxLength(200);
        builder.Property(batch => batch.FailureCode).HasMaxLength(100);
        builder.Property(batch => batch.FailureDetail).HasMaxLength(200);
        builder.Property(batch => batch.UnresolvedValuesJson)
            .HasColumnName("UnresolvedValues")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(batch => batch.Version).IsRowVersion();
        builder.Ignore(batch => batch.FileRetained);
        builder.Ignore(batch => batch.AccountedRows);

        builder.HasIndex(batch => batch.StorageKey).IsUnique().HasDatabaseName($"UX_{Table}_StorageKey");
        // The history page: most recent first.
        builder.HasIndex(batch => new { batch.CreatedAtUtc, batch.Id })
            .IsDescending(true, true)
            .HasDatabaseName($"IX_{Table}_CreatedAtUtc_Id");
        // The purge and the start-up recovery both select by state.
        builder.HasIndex(batch => new { batch.State, batch.ClosedAtUtc }).HasDatabaseName($"IX_{Table}_State_ClosedAtUtc");
        builder.HasIndex(batch => batch.Sha256).HasDatabaseName($"IX_{Table}_Sha256");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(batch => batch.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
