using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<CandidateDocument>
{
    public void Configure(EntityTypeBuilder<CandidateDocument> builder)
    {
        builder.ToTable("CND_Documents", table => table.HasCheckConstraint("CK_CND_Documents_Size", "\"Size\" > 0 AND \"Size\" <= 20971520"));
        builder.HasKey(document => document.Id);
        builder.Property(document => document.StorageKey).HasMaxLength(240).IsRequired();
        builder.HasIndex(document => document.StorageKey).IsUnique();
        builder.Property(document => document.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(document => document.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(document => document.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(document => document.DocumentType).HasMaxLength(80).IsRequired();
        builder.Property(document => document.SourceKey).HasMaxLength(200);
        builder.Property(document => document.ScanState).HasConversion<string>().HasMaxLength(32);
        builder.Property(document => document.Version).IsRowVersion();
        builder.HasIndex([nameof(CandidateDocument.CandidateId)], "IX_CND_Documents_CandidateId");
        // At most one primary document per candidate, as a database invariant rather than
        // something every write path has to remember. Named explicitly because it shares
        // its column with the plain lookup index above.
        builder.HasIndex([nameof(CandidateDocument.CandidateId)], "UX_CND_Documents_CandidateId_Primary")
            .IsUnique()
            .HasFilter("\"IsPrimary\"");
        builder.HasIndex(document => document.SourceKey)
            .IsUnique()
            .HasFilter("\"SourceKey\" IS NOT NULL")
            .HasDatabaseName("UX_CND_Documents_SourceKey");
        builder.HasOne<Candidate>().WithMany().HasForeignKey(document => document.CandidateId).OnDelete(DeleteBehavior.Restrict);
    }
}
