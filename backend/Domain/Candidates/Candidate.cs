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
    private Candidate() { }

    public Candidate(Guid id, string firstName, string lastName, DateTimeOffset createdAtUtc)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Status = CandidateStatuses.New;
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
    public string Availability { get; private set; } = string.Empty;
    public string Status { get; private set; } = CandidateStatuses.New;
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
        string availability,
        string status,
        string source,
        string notes,
        DateTimeOffset updatedAtUtc)
    {
        if (!CandidateStatuses.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
        Phone = phone.Trim();
        Email = email.Trim();
        Location = location.Trim();
        Province = province.Trim();
        Country = country.Trim();
        Availability = availability.Trim();
        Status = status;
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
