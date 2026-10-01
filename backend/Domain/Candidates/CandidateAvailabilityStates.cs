namespace KeplerTalento.Domain.Candidates;

/// <summary>
/// The closed set of availability check values (KTL-36). These map one-to-one onto the
/// <c>CandidateAvailabilityState</c> union in the frontend and carry a check constraint in
/// PostgreSQL, so the set is enforced by the database rather than by convention.
/// </summary>
public static class CandidateAvailabilityStates
{
    public const string Unknown = "unknown";
    public const string Available = "available";
    public const string Unavailable = "unavailable";

    public static readonly IReadOnlyList<string> All =
    [
        Unknown,
        Available,
        Unavailable,
    ];

    public static bool IsKnown(string? state) =>
        state is not null && All.Contains(state, StringComparer.Ordinal);
}
