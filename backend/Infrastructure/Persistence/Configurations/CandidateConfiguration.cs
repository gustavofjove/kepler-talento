using KeplerTalento.Domain.Candidates;
using KeplerTalento.Infrastructure.Encryption;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public const string Table = "CND_Candidates";

    /// <summary>Shadow property and column holding the e-mail blind index.</summary>
    public const string EmailHash = "EmailHash";

    public static readonly string StatusCheckConstraint =
        "\"Status\" IN (" + string.Join(", ", CandidateStatuses.All.Select(status => $"'{status}'")) + ")";

    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable(Table, table =>
        {
            // KTL-33: the names are ciphertext, so a length check could no longer see an empty
            // name. The validators require one; NOT NULL still refuses a missing value.
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
        // KTL-33: personal fields are stored encrypted; the former column widths are the limits.
        builder.Property(candidate => candidate.FirstName).IsEncrypted(Table, CandidateTextLimits.FirstName, requireText: true).IsRequired();
        builder.Property(candidate => candidate.LastName).IsEncrypted(Table, CandidateTextLimits.LastName, requireText: true).IsRequired();
        builder.Property(candidate => candidate.Phone).IsEncrypted(Table, CandidateTextLimits.Phone).IsRequired();
        builder.Property(candidate => candidate.Email).IsEncrypted(Table, CandidateTextLimits.Email).IsRequired();
        builder.Property(candidate => candidate.Location).IsEncrypted(Table, CandidateTextLimits.Location).IsRequired();
        builder.Property(candidate => candidate.Province).IsEncrypted(Table, CandidateTextLimits.Province).IsRequired();
        builder.Property(candidate => candidate.Country).IsEncrypted(Table, CandidateTextLimits.Country).IsRequired();
        builder.Property(candidate => candidate.Availability).IsEncrypted(Table, CandidateTextLimits.Availability).IsRequired();
        builder.Property(candidate => candidate.Status).HasMaxLength(20).IsRequired();
        builder.Property(candidate => candidate.Source).IsEncrypted(Table, CandidateTextLimits.Source).IsRequired();
        builder.Property(candidate => candidate.Notes).IsEncrypted(Table).IsRequired();
        builder.Property(candidate => candidate.SourceKey).HasMaxLength(200);
        builder.Property(candidate => candidate.Version).IsRowVersion();
        // The e-mail blind index (KTL-33 design decision 5). A shadow property: the domain never
        // sees it, and the repository keeps it in step with the e-mail on every write.
        builder.Property<string>(EmailHash).HasMaxLength(100).IsRequired();
        builder.HasIndex(EmailHash).HasDatabaseName("IX_CND_Candidates_EmailHash");
        // The list screen reads active candidates most-recently-updated first.
        builder.HasIndex(candidate => new { candidate.IsActive, candidate.UpdatedAtUtc })
            .HasDatabaseName("IX_CND_Candidates_IsActive_UpdatedAtUtc");
        // The contracted list/search sort fields (KTL-18), each ending in the identifier
        // tie-breaker so an ordered page can be read from the index without a sort step. The
        // last-name sort has no index since KTL-33: names are ciphertext, sorted in the API.
        builder.HasIndex(candidate => new { candidate.IsActive, candidate.Status, candidate.Id })
            .HasDatabaseName("IX_CND_Candidates_IsActive_Status_Id");
        builder.HasIndex(candidate => candidate.SourceKey)
            .IsUnique()
            .HasFilter("\"SourceKey\" IS NOT NULL")
            .HasDatabaseName("UX_CND_Candidates_SourceKey");
    }
}
