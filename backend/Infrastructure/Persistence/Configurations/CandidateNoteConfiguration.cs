using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateNoteConfiguration : IEntityTypeConfiguration<CandidateNote>
{
    public const string Table = "CND_CandidateNotes";

    public void Configure(EntityTypeBuilder<CandidateNote> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_Body",
                $"char_length(btrim(\"Body\")) BETWEEN 1 AND {CandidateNote.MaximumBodyLength}");
            table.HasCheckConstraint(
                $"CK_{Table}_Deleted",
                "(\"IsActive\" AND \"DeletedAtUtc\" IS NULL) OR (NOT \"IsActive\" AND \"DeletedAtUtc\" IS NOT NULL)");
        });
        builder.HasKey(note => note.Id);
        builder.Property(note => note.Body).HasMaxLength(CandidateNote.MaximumBodyLength).IsRequired();
        builder.Property(note => note.CreatedAtUtc).IsRequired();
        builder.Property(note => note.UpdatedAtUtc).IsRequired();
        builder.Property(note => note.IsActive).IsRequired();
        builder.Property(note => note.Version).IsRowVersion();
        builder.HasOne<Candidate>()
            .WithMany(candidate => candidate.CustomNotes)
            .HasForeignKey(note => note.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(note => note.Author)
            .WithMany()
            .HasForeignKey(note => note.AuthorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(note => new { note.CandidateId, note.CreatedAtUtc })
            .IsDescending(false, true)
            .HasFilter("\"IsActive\"")
            .HasDatabaseName("IX_CND_CandidateNotes_CandidateId_CreatedAtUtc_Active");
    }
}
