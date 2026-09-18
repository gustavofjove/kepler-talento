using KeplerTalento.Domain.Catalogs;

namespace KeplerTalento.Domain.Candidates;

/// <summary>A governed tag assigned to a candidate.</summary>
public sealed class CandidateTag : CandidateRelation
{
    private CandidateTag() { }

    public CandidateTag(Guid id, Guid candidateId, Guid tagId)
        : base(id, candidateId)
    {
        TagId = tagId;
    }

    public Guid TagId { get; private set; }
    public string TagFamily { get; private set; } = CatalogFamilies.Tag;

    public void SetValue(Guid tagId) => TagId = tagId;
}
