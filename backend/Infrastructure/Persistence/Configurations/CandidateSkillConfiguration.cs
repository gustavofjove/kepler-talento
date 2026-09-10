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
        builder.ConfigureRelation(Table, candidate => candidate.Skills);
        builder.HasCatalogReference(
            skill => new { skill.SkillId, skill.SkillFamily },
            skill => skill.SkillFamily);
        builder.HasCatalogReference(
            skill => new { skill.LevelId, skill.LevelFamily },
            skill => skill.LevelFamily);
        // KTL-10 deliberately adds no search index here. Search reads this table in the
        // opposite direction from everything else — a criterion names the skill and asks
        // which candidates hold it — but the composite catalog foreign key above already
        // carries an index led by "SkillId", and the retained query plans
        // (docs/ktl-10/query-plans.md) show the planner choosing it. A wider
        // (SkillId, CandidateId, LevelId) index was measured and never chosen, so it would
        // have cost every write and bought nothing.
    }
}
