using KeplerTalento.Domain.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class MigrationRunConfiguration : IEntityTypeConfiguration<MigrationRun>
{
    public static readonly string VerbCheckConstraint =
        "\"Verb\" IN (" + string.Join(", ", MigrationVerbs.All.Select(verb => $"'{verb}'")) + ")";

    public static readonly string OutcomeCheckConstraint =
        "\"Outcome\" IN (" + string.Join(", ", MigrationOutcomes.All.Select(outcome => $"'{outcome}'")) + ")";

    public void Configure(EntityTypeBuilder<MigrationRun> builder)
    {
        builder.ToTable("OPS_MigrationRuns", table =>
        {
            table.HasCheckConstraint("CK_OPS_MigrationRuns_Verb", VerbCheckConstraint);
            table.HasCheckConstraint("CK_OPS_MigrationRuns_Outcome", OutcomeCheckConstraint);
            // A load must name the backup that undoes it; validate and report write nothing,
            // so they need none.
            table.HasCheckConstraint(
                "CK_OPS_MigrationRuns_BackupLabel",
                "\"Verb\" <> 'load' OR \"BackupLabel\" IS NOT NULL");
        });
        builder.HasKey(run => run.Id);
        builder.Property(run => run.Verb).HasMaxLength(20).IsRequired();
        builder.Property(run => run.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(run => run.BackupLabel).HasMaxLength(200);
        builder.HasIndex(run => run.StartedAtUtc).HasDatabaseName("IX_OPS_MigrationRuns_StartedAtUtc");
    }
}
