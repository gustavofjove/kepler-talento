using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// A language a candidate declares, at a declared proficiency level. Both values
/// reference catalog entries; the family columns are fixed so the composite foreign key
/// cannot resolve to an entry of the wrong family.
/// </summary>
public sealed class CandidateLanguage : CandidateRelation
{
    private CandidateLanguage() { }

    public CandidateLanguage(Guid id, Guid candidateId, Guid languageId, Guid levelId)
        : base(id, candidateId)
    {
        LanguageId = languageId;
        LevelId = levelId;
    }

    public Guid LanguageId { get; private set; }
    public string LanguageFamily { get; private set; } = CatalogFamilies.Language;
    public Guid LevelId { get; private set; }
    public string LevelFamily { get; private set; } = CatalogFamilies.LanguageLevel;
    public string? Certification { get; private set; }

    public void SetCertification(string? certification) =>
        Certification = string.IsNullOrWhiteSpace(certification) ? null : certification.Trim();

    /// <summary>
    /// Re-points an existing row at different catalog entries. Editing in place rather
    /// than replacing the row keeps its identifier and its migration provenance, which a
    /// delete-and-reinsert would discard on the first edit.
    /// </summary>
    public void SetValues(Guid languageId, Guid levelId)
    {
        LanguageId = languageId;
        LevelId = levelId;
    }
}
