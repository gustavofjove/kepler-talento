namespace KeplerTalento.Domain.Positions;

/// <summary>
/// The closed set of stages a candidate can reach within one position (KTL-30). The order of
/// <see cref="All"/> is the display order. A stage is independent of the candidate's own status.
/// </summary>
public static class PositionCandidateStages
{
    public const string New = "new";
    public const string Shortlisted = "shortlisted";
    public const string Interview = "interview";
    public const string Hired = "hired";
    public const string Rejected = "rejected";

    public static IReadOnlyList<string> All { get; } = [New, Shortlisted, Interview, Hired, Rejected];

    public static bool IsKnown(string? value) => value is not null && All.Contains(value, StringComparer.Ordinal);
}

/// <summary>
/// A persistent link between one candidate and one position. It lives outside the
/// <see cref="Position"/> aggregate so that a stage change never bumps the position's version
/// (KTL-30 design D1). Removal is a physical delete; rejection is the <c>rejected</c> stage.
/// </summary>
public sealed class PositionCandidate
{
    /// <summary>The most links one position may hold, which keeps its unpaged list bounded.</summary>
    public const int MaximumPerPosition = 500;

    private PositionCandidate() { }

    public PositionCandidate(Guid id, Guid positionId, Guid candidateId, DateTimeOffset addedAtUtc)
    {
        Id = id;
        PositionId = positionId;
        CandidateId = candidateId;
        Stage = PositionCandidateStages.New;
        AddedAtUtc = addedAtUtc;
        UpdatedAtUtc = addedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid PositionId { get; private set; }
    public Guid CandidateId { get; private set; }
    public string Stage { get; private set; } = PositionCandidateStages.New;
    public DateTimeOffset AddedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public uint Version { get; private set; }

    public void ChangeStage(string stage, DateTimeOffset updatedAtUtc)
    {
        if (!PositionCandidateStages.IsKnown(stage)) throw new ArgumentOutOfRangeException(nameof(stage));
        Stage = stage;
        UpdatedAtUtc = updatedAtUtc < AddedAtUtc ? AddedAtUtc : updatedAtUtc;
    }

    /// <summary>The audit subject: both opaque ids, since the link itself may later be deleted.</summary>
    public static string AuditSubject(Guid positionId, Guid candidateId) =>
        $"{positionId:N}:{candidateId:N}";
}
