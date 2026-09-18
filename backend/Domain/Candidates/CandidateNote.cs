using KeplerTalento.Domain.Identity;

namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// One independently versioned note in a candidate's non-destructive history.
/// </summary>
public sealed class CandidateNote
{
    public const int MaximumBodyLength = 4000;

    private CandidateNote() { }

    public CandidateNote(
        Guid id,
        Guid candidateId,
        string body,
        Guid? authorUserId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CandidateId = candidateId;
        Body = NormalizeBody(body);
        AuthorUserId = authorUserId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CandidateId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public Guid? AuthorUserId { get; private set; }
    public User? Author { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void Edit(string body, DateTimeOffset updatedAtUtc)
    {
        Body = NormalizeBody(body);
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetActive(bool isActive, DateTimeOffset updatedAtUtc)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        DeletedAtUtc = isActive ? null : updatedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeBody(string body)
    {
        var normalized = body?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > MaximumBodyLength)
        {
            throw new ArgumentException("The note body must contain between 1 and 4000 characters.", nameof(body));
        }
        return normalized;
    }
}
