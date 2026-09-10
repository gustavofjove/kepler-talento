namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// The closed set of candidate statuses. These map one-to-one onto the
/// <c>CandidateStatus</c> union in the frontend and carry a check constraint in
/// PostgreSQL, so the set is enforced by the database rather than by convention.
/// </summary>
public static class CandidateStatuses
{
    public const string New = "new";
    public const string Available = "available";
    public const string InProcess = "in_process";
    public const string Hired = "hired";
    public const string Rejected = "rejected";

    public static readonly IReadOnlyList<string> All =
    [
        New,
        Available,
        InProcess,
        Hired,
        Rejected,
    ];

    public static bool IsKnown(string? status) =>
        status is not null && All.Contains(status, StringComparer.Ordinal);
}
