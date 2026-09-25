using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Positions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class PositionCandidateConfiguration : IEntityTypeConfiguration<PositionCandidate>
{
    public const string Table = "OPS_PositionCandidates";

    public void Configure(EntityTypeBuilder<PositionCandidate> builder)
    {
        var stages = string.Join(", ", PositionCandidateStages.All.Select(stage => $"'{stage}'"));
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint($"CK_{Table}_Stage", $"\"Stage\" IN ({stages})");
            table.HasCheckConstraint($"CK_{Table}_Timestamps", "\"UpdatedAtUtc\" >= \"AddedAtUtc\"");
        });
        builder.HasKey(link => link.Id);
        builder.Property(link => link.Stage).HasMaxLength(32).IsRequired();
        builder.Property(link => link.Version).IsRowVersion();
        // Both references restrict: a link never disappears because its position or candidate did.
        builder.HasOne<Position>().WithMany().HasForeignKey(link => link.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Candidate>().WithMany().HasForeignKey(link => link.CandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.PositionId, link.CandidateId }).IsUnique()
            .HasDatabaseName("UX_OPS_PositionCandidates_PositionId_CandidateId");
        builder.HasIndex(link => link.CandidateId).HasDatabaseName("IX_OPS_PositionCandidates_CandidateId");
    }
}
