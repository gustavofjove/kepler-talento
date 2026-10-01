using KeplerTalento.Domain.Identity;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// The candidate aggregate root. Identity, contact details, location, notes, and the
/// consent and retention metadata are personal data.
/// </summary>
/// <remarks>
/// Consent and retention dates are nullable on purpose: an absent date stays absent
/// rather than acquiring a permissive default such as today. A candidate whose consent
/// metadata cannot be established is rejected by its writer, never defaulted here.
/// </remarks>
public sealed class Candidate
{
    private readonly List<CandidateLanguage> _languages = [];
    private readonly List<CandidateProgram> _programs = [];
    private readonly List<CandidateEducation> _education = [];
    private readonly List<CandidateExperience> _experience = [];
    private readonly List<CandidateSkill> _skills = [];
    private readonly List<CandidateTag> _tags = [];
    private readonly List<CandidateNote> _customNotes = [];

    private Candidate() { }

    public Candidate(Guid id, string firstName, string lastName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public string Province { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public DateOnly? ReceivedAt { get; private set; }
    public DateOnly? ConsentAt { get; private set; }
    public DateOnly? ReviewDueAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>
    /// The latest availability check (KTL-36): one of <see cref="CandidateAvailabilityStates"/>.
    /// Only the latest check is kept; it changes only through <see cref="RecordAvailability"/>.
    /// </summary>
    public string AvailabilityState { get; private set; } = CandidateAvailabilityStates.Unknown;

    /// <summary>The calendar day the check was made. Null exactly when the state is unknown.</summary>
    public DateOnly? AvailabilityCheckedOn { get; private set; }

    /// <summary>The day until which the candidate is unavailable. Only on an unavailable check.</summary>
    public DateOnly? AvailabilityUntil { get; private set; }

    /// <summary>
    /// Who recorded the check. Nullable even for a known check, because the current actor may
    /// have no stored user (the development actor); always null for an unknown check.
    /// </summary>
    public Guid? AvailabilityCheckedByUserId { get; private set; }
    public User? CheckedBy { get; private set; }

    /// <summary>
    /// Provenance of a record loaded from the legacy Access dataset. Null for records the
    /// application created. Not personal data; it is what makes a migration re-run
    /// idempotent and its reconciliation report citable.
    /// </summary>
    public string? SourceKey { get; private set; }

    /// <summary>
    /// When the migration last wrote this record from the legacy dataset. Null for records
    /// the application created.
    /// </summary>
    /// <remarks>
    /// This is the guard against a later migration re-run silently overwriting work done
    /// in the application. The migration stamps it with the same instant it stamps
    /// <see cref="UpdatedAtUtc"/>, so any subsequent application write moves
    /// <see cref="UpdatedAtUtc"/> past it and becomes detectable.
    /// </remarks>
    public DateTimeOffset? SourceLoadedAtUtc { get; private set; }

    /// <summary>
    /// True when something other than the migration has written this record since the
    /// migration last loaded it. A re-run reports these rows and leaves them alone unless
    /// the operator explicitly asks for them to be overwritten.
    /// </summary>
    public bool HasApplicationChangesSinceLoad =>
        SourceLoadedAtUtc is not null && UpdatedAtUtc > SourceLoadedAtUtc;

    public uint Version { get; private set; }

    /// <summary>
    /// The collections this candidate owns. They are replaced as whole sets — see
    /// <see cref="ReplaceLanguages"/> and its siblings — because a per-item write has no
    /// token of its own to check a concurrent editor against; the candidate's
    /// <see cref="Version"/> is the concurrency token for everything it owns.
    /// </summary>
    public IReadOnlyList<CandidateLanguage> Languages => _languages;
    public IReadOnlyList<CandidateProgram> Programs => _programs;
    public IReadOnlyList<CandidateEducation> Education => _education;
    public IReadOnlyList<CandidateExperience> Experience => _experience;
    public IReadOnlyList<CandidateSkill> Skills => _skills;
    public IReadOnlyList<CandidateTag> Tags => _tags;
    public IReadOnlyList<CandidateNote> CustomNotes => _customNotes;

    /// <summary>
    /// Replaces a collection wholesale and advances the update timestamp. The records
    /// leaving the collection are returned so the caller can delete them explicitly:
    /// the relation mappings deliberately refuse cascade deletion, because a candidate's
    /// removal is logical and must never destroy the records it owns.
    /// </summary>
    public IReadOnlyList<CandidateLanguage> ReplaceLanguages(
        IEnumerable<CandidateLanguage> languages,
        DateTimeOffset updatedAtUtc) => Replace(_languages, languages, updatedAtUtc);

    public IReadOnlyList<CandidateProgram> ReplacePrograms(
        IEnumerable<CandidateProgram> programs,
        DateTimeOffset updatedAtUtc) => Replace(_programs, programs, updatedAtUtc);

    public IReadOnlyList<CandidateEducation> ReplaceEducation(
        IEnumerable<CandidateEducation> education,
        DateTimeOffset updatedAtUtc) => Replace(_education, education, updatedAtUtc);

    public IReadOnlyList<CandidateExperience> ReplaceExperience(
        IEnumerable<CandidateExperience> experience,
        DateTimeOffset updatedAtUtc) => Replace(_experience, experience, updatedAtUtc);

    public IReadOnlyList<CandidateSkill> ReplaceSkills(
        IEnumerable<CandidateSkill> skills,
        DateTimeOffset updatedAtUtc) => Replace(_skills, skills, updatedAtUtc);

    public IReadOnlyList<CandidateTag> ReplaceTags(
        IEnumerable<CandidateTag> tags,
        DateTimeOffset updatedAtUtc) => Replace(_tags, tags, updatedAtUtc);

    private List<TRelation> Replace<TRelation>(
        List<TRelation> current,
        IEnumerable<TRelation> replacement,
        DateTimeOffset updatedAtUtc)
        where TRelation : CandidateRelation
    {
        var incoming = replacement.ToList();
        if (incoming.Any(relation => relation.CandidateId != Id))
        {
            throw new InvalidOperationException("A relation belongs to a different candidate.");
        }
        var removed = current.Where(existing => incoming.All(item => item.Id != existing.Id)).ToList();
        current.Clear();
        current.AddRange(incoming);
        UpdatedAtUtc = updatedAtUtc;
        return removed;
    }

    /// <summary>
    /// Applies the editable field set of a candidate, leaving identity, consent and
    /// retention metadata alone. The availability check travels through
    /// <see cref="RecordAvailability"/>, never through this method.
    /// </summary>
    public void UpdateDetails(
        string firstName,
        string lastName,
        string phone,
        string email,
        string location,
        string province,
        string country,
        string source,
        string notes,
        DateTimeOffset updatedAtUtc)
    {
        SetIdentity(firstName, lastName, updatedAtUtc);
        SetDetails(phone, email, location, province, country, source, notes, updatedAtUtc);
    }

    /// <summary>
    /// Records an availability check, replacing the previous one. The invariants are refused
    /// here as well as by the validator and the database check constraints, so an inconsistent
    /// check can never be stored whichever path writes it. Recording <c>unknown</c> clears the
    /// dates and the checker.
    /// </summary>
    public void RecordAvailability(
        string state,
        DateOnly? checkedOn,
        DateOnly? until,
        Guid? checkedByUserId,
        DateTimeOffset updatedAtUtc)
    {
        if (!CandidateAvailabilityStates.IsKnown(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state == CandidateAvailabilityStates.Unknown)
        {
            if (checkedOn is not null || until is not null)
            {
                throw new ArgumentException("An unknown availability carries no dates.", nameof(checkedOn));
            }
            AvailabilityState = state;
            AvailabilityCheckedOn = null;
            AvailabilityUntil = null;
            AvailabilityCheckedByUserId = null;
            UpdatedAtUtc = updatedAtUtc;
            return;
        }

        if (checkedOn is null)
        {
            throw new ArgumentException("A known availability needs its check date.", nameof(checkedOn));
        }
        if (until is not null && (state != CandidateAvailabilityStates.Unavailable || until < checkedOn))
        {
            throw new ArgumentException(
                "An until date is only valid on an unavailable check, on or after the check date.",
                nameof(until));
        }

        AvailabilityState = state;
        AvailabilityCheckedOn = checkedOn;
        AvailabilityUntil = until;
        AvailabilityCheckedByUserId = checkedByUserId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetIdentity(string firstName, string lastName, DateTimeOffset updatedAtUtc)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetDetails(
        string phone,
        string email,
        string location,
        string province,
        string country,
        string source,
        string notes,
        DateTimeOffset updatedAtUtc)
    {
        Phone = phone.Trim();
        Email = email.Trim();
        Location = location.Trim();
        Province = province.Trim();
        Country = country.Trim();
        Source = source.Trim();
        Notes = notes;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>
    /// Sets the consent and retention metadata exactly as supplied. Passing null clears a
    /// date; no value is ever substituted for an absent one.
    /// </summary>
    public void SetConsent(
        DateOnly? receivedAt,
        DateOnly? consentAt,
        DateOnly? reviewDueAt,
        DateTimeOffset updatedAtUtc)
    {
        ReceivedAt = receivedAt;
        ConsentAt = consentAt;
        ReviewDueAt = reviewDueAt;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetSourceKey(string? sourceKey) =>
        SourceKey = string.IsNullOrWhiteSpace(sourceKey) ? null : sourceKey.Trim();

    /// <summary>
    /// Advances the update timestamp for a change to something the candidate owns whose
    /// own fields live elsewhere — its documents. Without this the row would not be
    /// modified, and the version check that protects the whole aggregate would not run.
    /// </summary>
    public void TouchUpdated(DateTimeOffset updatedAtUtc) => UpdatedAtUtc = updatedAtUtc;

    /// <summary>
    /// Records that the migration wrote this record from the legacy dataset, at the same
    /// instant it stamped <see cref="UpdatedAtUtc"/>. Call it last, after the setters.
    /// </summary>
    public void MarkSourceLoaded(DateTimeOffset loadedAtUtc)
    {
        if (SourceKey is null)
        {
            throw new InvalidOperationException("A record without a source key was never loaded from the legacy dataset.");
        }
        UpdatedAtUtc = loadedAtUtc;
        SourceLoadedAtUtc = loadedAtUtc;
    }

    /// <summary>
    /// Logical removal. The row and its relations are preserved and the candidate stays
    /// retrievable by identifier; nothing is physically deleted.
    /// </summary>
    public void Deactivate(DateTimeOffset deletedAtUtc)
    {
        if (!IsActive)
        {
            return;
        }
        IsActive = false;
        DeletedAtUtc = deletedAtUtc;
        UpdatedAtUtc = deletedAtUtc;
    }

    public void Reactivate(DateTimeOffset updatedAtUtc)
    {
        if (IsActive)
        {
            return;
        }
        IsActive = true;
        DeletedAtUtc = null;
        UpdatedAtUtc = updatedAtUtc;
    }
}
