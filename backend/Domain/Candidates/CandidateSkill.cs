using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// A skill a candidate declares, at a declared level. Both values reference catalog
/// entries in fixed families.
/// </summary>
public sealed class CandidateSkill : CandidateRelation
{
    private CandidateSkill() { }

    public CandidateSkill(Guid id, Guid candidateId, Guid skillId, Guid levelId)
        : base(id, candidateId)
    {
        SkillId = skillId;
        LevelId = levelId;
    }

    public Guid SkillId { get; private set; }
    public string SkillFamily { get; private set; } = CatalogFamilies.Skill;
    public Guid LevelId { get; private set; }
    public string LevelFamily { get; private set; } = CatalogFamilies.SkillLevel;
}
