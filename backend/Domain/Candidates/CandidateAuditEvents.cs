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

    /// <summary>
    /// No longer written since KTL-36 removed the candidate status. It stays in the catalogue so
    /// that events recorded before then can still be read and filtered.
    /// </summary>
    public const string StatusChanged = "candidate.status_changed";

    /// <summary>
    /// An availability check was recorded (KTL-36). Like every candidate event it carries the
    /// candidate id only, never the value or the dates.
    /// </summary>
    public const string AvailabilityChecked = "candidate.availability_checked";
    public const string Removed = "candidate.removed";
    public const string Restored = "candidate.restored";
    public const string RelationsChanged = "candidate.relations_changed";
    public const string TagsChanged = "candidate.tags_changed";
    public const string NoteAdded = "candidate.note_added";
    public const string NoteUpdated = "candidate.note_updated";
    public const string NoteRetired = "candidate.note_retired";
    public const string DocumentsChanged = "candidate.documents_changed";
    public const string Read = "candidate.read";

    /// <summary>
    /// A CV draft extraction attempt (KTL-32). The subject is the opaque draft id, never a
    /// candidate id, and the outcome is a code: nothing from the file is recorded.
    /// </summary>
    public const string CvDraftExtracted = "candidate.cv_draft.extracted";
}
