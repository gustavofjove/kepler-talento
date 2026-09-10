using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// A software or program a candidate declares, at a declared level. Both values
/// reference catalog entries in fixed families.
/// </summary>
public sealed class CandidateProgram : CandidateRelation
{
    private CandidateProgram() { }

    public CandidateProgram(Guid id, Guid candidateId, Guid programId, Guid levelId)
        : base(id, candidateId)
    {
        ProgramId = programId;
        LevelId = levelId;
    }

    public Guid ProgramId { get; private set; }
    public string ProgramFamily { get; private set; } = CatalogFamilies.Program;
    public Guid LevelId { get; private set; }
    public string LevelFamily { get; private set; } = CatalogFamilies.ProgramLevel;
    public int? YearsExperience { get; private set; }

    /// <summary>See <see cref="CandidateLanguage.SetValues"/>.</summary>
    public void SetValues(Guid programId, Guid levelId)
    {
        ProgramId = programId;
        LevelId = levelId;
    }

    public void SetYearsExperience(int? yearsExperience)
    {
        if (yearsExperience is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(yearsExperience));
        }
        YearsExperience = yearsExperience;
    }
}
