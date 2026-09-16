using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

/// <summary>
/// Per-row outcomes. There is no value column — the absence is the control — and no version
/// column, because an outcome is inserted once and never updated.
/// </summary>
public sealed class ImportRowOutcomeConfiguration : IEntityTypeConfiguration<ImportRowOutcome>
{
    public const string Table = "ADM_ImportRowOutcomes";

    /// <summary>
    /// One outcome per row per phase. This is what makes a resumed commit unable to load a row
    /// twice: the outcome insert and the candidate insert share a transaction, so a second attempt
    /// fails here and takes its candidate down with it.
    /// </summary>
    public const string UniqueRowIndex = "UX_ADM_ImportRowOutcomes_BatchId_Phase_RowNumber";

    public void Configure(EntityTypeBuilder<ImportRowOutcome> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_Phase",
                "\"Phase\" IN (" + string.Join(", ", ImportPhases.All.Select(phase => $"'{phase}'")) + ")");
            table.HasCheckConstraint(
                $"CK_{Table}_Outcome",
                "\"Outcome\" IN (" + string.Join(", ", ImportRowOutcomes.All.Select(outcome => $"'{outcome}'")) + ")");
            table.HasCheckConstraint(
                $"CK_{Table}_ReasonCode",
                "(\"ReasonCode\" IS NULL OR \"ReasonCode\" IN ("
                + string.Join(", ", ImportReasonCodes.RowCodes.Select(code => $"'{code}'"))
                + ")) AND (\"Outcome\" = 'loaded' OR \"ReasonCode\" IS NOT NULL)");
            table.HasCheckConstraint($"CK_{Table}_RowNumber", "\"RowNumber\" >= 1");
            table.HasCheckConstraint(
                $"CK_{Table}_Candidate",
                "\"CandidateId\" IS NULL OR (\"Outcome\" = 'loaded' AND \"Phase\" = 'commit')");
        });

        builder.HasKey(outcome => outcome.Id);
        builder.Property(outcome => outcome.Phase).HasMaxLength(12).IsRequired();
        builder.Property(outcome => outcome.Outcome).HasMaxLength(12).IsRequired();
        builder.Property(outcome => outcome.Field).HasMaxLength(64);
        builder.Property(outcome => outcome.ReasonCode).HasMaxLength(64);

        builder.HasIndex(outcome => new { outcome.BatchId, outcome.Phase, outcome.RowNumber })
            .IsUnique()
            .HasDatabaseName(UniqueRowIndex);
        builder.HasIndex(outcome => new { outcome.BatchId, outcome.RowNumber })
            .HasDatabaseName($"IX_{Table}_BatchId_RowNumber");
        builder.HasIndex(outcome => outcome.CandidateId)
            .HasFilter("\"CandidateId\" IS NOT NULL")
            .HasDatabaseName($"IX_{Table}_CandidateId");

        builder.HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(outcome => outcome.BatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Candidate>()
            .WithMany()
            .HasForeignKey(outcome => outcome.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
