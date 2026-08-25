using KeplerTalento.Domain.Candidates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("CND_Candidates", table => table.HasCheckConstraint("CK_CND_Candidates_Name", "char_length(\"FirstName\") > 0 AND char_length(\"LastName\") > 0"));
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.FirstName).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.LastName).HasMaxLength(180).IsRequired();
        builder.Property(candidate => candidate.Version).IsRowVersion();
        builder.HasIndex(candidate => new { candidate.LastName, candidate.FirstName });
    }
}
