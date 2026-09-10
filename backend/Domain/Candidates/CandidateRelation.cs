namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// Common shape of the collections owned by a candidate. Every relation belongs to
/// exactly one candidate and survives that candidate's logical removal, so a removed
/// candidate stays recoverable with its record intact.
/// </summary>
public abstract class CandidateRelation
{
    protected CandidateRelation() { }

    protected CandidateRelation(Guid id, Guid candidateId)
    {
        Id = id;
        CandidateId = candidateId;
    }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public string? Notes { get; protected set; }

    /// <summary>
    /// Provenance of a row loaded from the legacy Access dataset; null for rows the
    /// application created. See <see cref="Candidate.SourceKey"/>.
    /// </summary>
    public string? SourceKey { get; private set; }

    public void SetNotes(string? notes) =>
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public void SetSourceKey(string? sourceKey) =>
        SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim();
}
