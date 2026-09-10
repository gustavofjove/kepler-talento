using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateLanguageConfiguration : IEntityTypeConfiguration<CandidateLanguage>
{
    private const string Table = "CND_CandidateLanguages";

    public void Configure(EntityTypeBuilder<CandidateLanguage> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_LanguageFamily",
                CandidateRelationMapping.FamilyCheck("LanguageFamily", CatalogFamilies.Language));
            table.HasCheckConstraint(
                $"CK_{Table}_LevelFamily",
                CandidateRelationMapping.FamilyCheck("LevelFamily", CatalogFamilies.LanguageLevel));
        });
        builder.ConfigureRelation(Table, candidate => candidate.Languages);
        builder.Property(language => language.Certification).HasMaxLength(160);
        builder.HasCatalogReference(
            language => new { language.LanguageId, language.LanguageFamily },
            language => language.LanguageFamily);
        builder.HasCatalogReference(
            language => new { language.LevelId, language.LevelFamily },
            language => language.LevelFamily);
        // See CandidateSkillConfiguration: KTL-10 adds no search index here either. The
        // catalog foreign key's own index already leads with "LanguageId".
    }
}
