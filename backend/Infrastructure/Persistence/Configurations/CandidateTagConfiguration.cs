using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateTagConfiguration : IEntityTypeConfiguration<CandidateTag>
{
    private const string Table = "CND_CandidateTags";

    public void Configure(EntityTypeBuilder<CandidateTag> builder)
    {
        builder.ToTable(Table, table =>
            table.HasCheckConstraint(
                $"CK_{Table}_TagFamily",
                CandidateRelationMapping.FamilyCheck("TagFamily", CatalogFamilies.Tag)));
        builder.ConfigureRelation(Table, candidate => candidate.Tags);
        builder.HasIndex(tag => new { tag.CandidateId, tag.TagId })
            .IsUnique()
            .HasDatabaseName("UX_CND_CandidateTags_CandidateId_TagId");
        builder.HasCatalogReference(
            tag => new { tag.TagId, tag.TagFamily },
            tag => tag.TagFamily);
    }
}
