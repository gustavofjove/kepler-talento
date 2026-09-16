using KeplerTalento.Application.Import;
using KeplerTalento.Infrastructure.Documents;

namespace KeplerTalento.Infrastructure.Import;

/// <summary>
/// Import file configuration (design D6, D7). Import files share the KTL-9 storage and scanning
/// mechanism; what differs is here — their own size limit, row limit and retention.
/// </summary>
public sealed class ImportOptions
{
    public const string SectionName = "Import";

    public long MaximumBytes { get; init; } = 5 * 1024 * 1024;

    public int MaximumRows { get; init; } = CandidateImportContract.DefaultMaximumRows;

    /// <summary>Days after a batch closes before its uploaded file is purged.</summary>
    public int RetentionDays { get; init; } = 30;

    /// <summary>How often the worker queues a purge pass.</summary>
    public int PurgeIntervalMinutes { get; init; } = 60;

    /// <summary>
    /// How old an import file with no batch must be before the purge removes it. Covers an upload
    /// interrupted between writing the file and recording its batch.
    /// </summary>
    public int OrphanGraceHours { get; init; } = 24;

    public void Validate()
    {
        if (MaximumBytes is <= 0 or > DocumentStorageOptions.AbsoluteMaximumBytes
            || MaximumRows is < 1 or > 50_000
            || RetentionDays is < 1 or > 3650
            || PurgeIntervalMinutes is < 1 or > 1440
            || OrphanGraceHours is < 1 or > 720)
        {
            throw new InvalidOperationException("Import configuration is unsafe.");
        }
    }
}
