namespace KeplerTalento.Domain.Candidates;

public sealed class Candidate
{
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
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public uint Version { get; private set; }
}
