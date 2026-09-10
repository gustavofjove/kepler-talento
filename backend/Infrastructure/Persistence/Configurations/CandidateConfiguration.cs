using KeplerTalento.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public static readonly string StatusCheckConstraint =
        "\"Status\" IN (" + string.Join(", ", CandidateStatuses.All.Select(status => $"'{status}'")) + ")";

    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("CND_Candidates", table =>
        {
            table.HasCheckConstraint(
                "CK_CND_Candidates_Name",
                "char_length(\"FirstName\") > 0 AND char_length(\"LastName\") > 0");
            table.HasCheckConstraint("CK_CND_Candidates_Status", StatusCheckConstraint);
            // Logical removal is the only removal: an inactive candidate carries the moment
            // it was removed, and an active one carries none.
            table.HasCheckConstraint(
                "CK_CND_Candidates_Deleted",
                "(\"IsActive\" AND \"DeletedAtUtc\" IS NULL) OR (NOT \"IsActive\" AND \"DeletedAtUtc\" IS NOT NULL)");
            // A record cannot have been loaded from the legacy dataset without carrying the
            // provenance that says which source row it came from.
            table.HasCheckConstraint(
                "CK_CND_Candidates_SourceLoaded",
                "\"SourceLoadedAtUtc\" IS NULL OR \"SourceKey\" IS NOT NULL");
        });
        builder.HasKey(candidate => candidate.Id);
        // Derived from UpdatedAtUtc and SourceLoadedAtUtc; nothing to persist.
        builder.Ignore(candidate => candidate.HasApplicationChangesSinceLoad);
        builder.Property(candidate => candidate.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.LastName).HasMaxLength(180).IsRequired();
        builder.Property(candidate => candidate.Phone).HasMaxLength(40).IsRequired();
        builder.Property(candidate => candidate.Email).HasMaxLength(255).IsRequired();
        builder.Property(candidate => candidate.Location).HasMaxLength(160).IsRequired();
        builder.Property(candidate => candidate.Province).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.Country).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.Availability).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.Status).HasMaxLength(20).IsRequired();
        builder.Property(candidate => candidate.Source).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.Notes).IsRequired();
        builder.Property(candidate => candidate.SourceKey).HasMaxLength(200);
        builder.Property(candidate => candidate.Version).IsRowVersion();
        builder.HasIndex(candidate => new { candidate.LastName, candidate.FirstName });
        // The list screen reads active candidates most-recently-updated first.
        builder.HasIndex(candidate => new { candidate.IsActive, candidate.UpdatedAtUtc })
            .HasDatabaseName("IX_CND_Candidates_IsActive_UpdatedAtUtc");
        builder.HasIndex(candidate => candidate.SourceKey)
            .IsUnique()
            .HasFilter("\"SourceKey\" IS NOT NULL")
            .HasDatabaseName("UX_CND_Candidates_SourceKey");
    }
}
