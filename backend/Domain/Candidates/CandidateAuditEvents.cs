namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// Audit event types recorded for candidates.
/// </summary>
/// <remarks>
/// The type is the whole description of what happened. An audit event carries the actor's
/// internal user id, the candidate identifier, one of these types and the request correlation
/// identifier — never a field value, because every interesting candidate field is personal data
/// and an audit trail that reproduces it is a second copy of the record with a longer retention.
/// Opening one candidate's record is audited as <see cref="Read"/> since KTL-19; searching and
/// listing are not, because they name no individual (KTL-19 design D3).
/// </remarks>
public static class CandidateAuditEvents
{
    public const string Created = "candidate.created";
    public const string Updated = "candidate.updated";
    public const string StatusChanged = "candidate.status_changed";
    public const string Removed = "candidate.removed";
    public const string Restored = "candidate.restored";
    public const string RelationsChanged = "candidate.relations_changed";
    public const string DocumentsChanged = "candidate.documents_changed";
    public const string Read = "candidate.read";
}
