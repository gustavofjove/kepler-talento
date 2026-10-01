namespace KeplerTalento.Tools.DataMigration.Loading;

/// <summary>
/// The five candidate status codes of the Access export contract, with the Spanish labels Access
/// stores (see <c>docs/ktl-7/access-export-procedure.md</c>).
/// </summary>
/// <remarks>
/// The application has had no candidate status since KTL-36. The export contract still carries
/// the column, and validation still refuses a code outside this set, but the loader keeps the
/// value only as note text (<see cref="LegacyNotes"/>).
/// </remarks>
public static class LegacyCandidateStatuses
{
    public const string New = "new";
    public const string Available = "available";
    public const string InProcess = "in_process";
    public const string Hired = "hired";
    public const string Rejected = "rejected";

    private static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [New] = "Nuevo",
        [Available] = "Disponible",
        [InProcess] = "En proceso",
        [Hired] = "Contratado",
        [Rejected] = "Descartado",
    };

    public static readonly IReadOnlyList<string> All = [New, Available, InProcess, Hired, Rejected];

    public static bool IsKnown(string? status) => status is not null && Labels.ContainsKey(status);

    public static string Label(string status) =>
        Labels.TryGetValue(status, out var label) ? label : throw new ArgumentOutOfRangeException(nameof(status));
}

/// <summary>
/// Composes a migrated candidate's notes from the legacy row (KTL-36 design D9).
/// </summary>
/// <remarks>
/// A pure function of the row, so a re-run recomposes the same text instead of appending the
/// lines again. The values are personal data like any other note: nothing here logs them, and
/// the reconciliation report never carries notes.
/// </remarks>
public static class LegacyNotes
{
    public static string Compose(string notes, string status, string availability)
    {
        var lines = new List<string>(2);
        var code = status.Trim();
        if (code.Length > 0 && code != LegacyCandidateStatuses.New)
        {
            lines.Add($"Estado en Access: {LegacyCandidateStatuses.Label(code)}.");
        }
        if (!string.IsNullOrWhiteSpace(availability))
        {
            lines.Add($"Disponibilidad en Access: {availability.Trim()}");
        }

        if (lines.Count == 0)
        {
            return notes;
        }
        var appended = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(notes) ? appended : $"{notes.TrimEnd()}\n\n{appended}";
    }
}
