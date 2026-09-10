using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KeplerTalento.Infrastructure.Persistence.Configurations;

public sealed class CandidateSkillConfiguration : IEntityTypeConfiguration<CandidateSkill>
{
    private const string Table = "CND_CandidateSkills";

    public void Configure(EntityTypeBuilder<CandidateSkill> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                $"CK_{Table}_SkillFamily",
                CandidateRelationMapping.FamilyCheck("SkillFamily", CatalogFamilies.Skill));
            table.HasCheckConstraint(
                $"CK_{Table}_LevelFamily",
                CandidateRelationMapping.FamilyCheck("LevelFamily", CatalogFamilies.SkillLevel));
        });
        builder.ConfigureRelation(Table);
        builder.HasCatalogReference(
            skill => new { skill.SkillId, skill.SkillFamily },
            skill => skill.SkillFamily);
        builder.HasCatalogReference(
            skill => new { skill.LevelId, skill.LevelFamily },
            skill => skill.LevelFamily);
    }
}
