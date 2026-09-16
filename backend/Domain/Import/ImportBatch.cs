namespace KeplerTalento.Domain.Import;

/// <summary>
/// The closed vocabulary of batch states. Stored as these strings and held to them by a check
/// constraint, so a state outside the set is unstorable whichever path writes it.
/// </summary>
public static class ImportBatchStates
{
    public const string Uploaded = "uploaded";
    public const string Scanning = "scanning";
    public const string Scanned = "scanned";
    public const string Validating = "validating";
    public const string Validated = "validated";
    public const string Committing = "committing";
    public const string Committed = "committed";
    public const string Infected = "infected";
    public const string Unscannable = "unscannable";
    public const string Failed = "failed";
    public const string Expired = "expired";

    public static readonly IReadOnlyList<string> All =
    [
        Uploaded,
        Scanning,
        Scanned,
        Validating,
        Validated,
        Committing,
        Committed,
        Infected,
        Unscannable,
        Failed,
        Expired,
    ];

    /// <summary>States a worker is expected to move on without a caller asking.</summary>
    public static readonly IReadOnlyList<string> Transient = [Uploaded, Scanning, Validating, Committing];

    /// <summary>
    /// States after which the uploaded file is no longer needed for anything but re-examination,
    /// and from which the retention window counts. <see cref="Validated"/> is included because
    /// a batch abandoned after validation must not keep bulk personal data forever.
    /// </summary>
    public static readonly IReadOnlyList<string> Closed = [Validated, Committed, Failed, Infected, Unscannable];

    public static bool IsKnown(string? state) => state is not null && All.Contains(state, StringComparer.Ordinal);
}

/// <summary>
/// One uploaded import file and everything that happened to it.
/// </summary>
/// <remarks>
/// <para>
/// The batch carries the state machine from the KTL-17 design (D3):
/// <c>uploaded → scanning → scanned → validating → validated → committing → committed</c>,
/// with <c>infected</c> and <c>unscannable</c> as terminal refusals of the file,
/// <c>failed</c> as the terminal outcome of a validation or commit that could not complete, and
/// <c>expired</c> once the file has been purged. Every transition not on that graph throws.
/// </para>
/// <para>
/// The batch id is the idempotency key for commit: claiming <c>validated → committing</c> is an
/// optimistic-concurrency write, so two concurrent commits cannot both proceed.
/// </para>
/// <para>
/// The storage key and the original filename are never logged. The filename is caller-supplied
/// text that routinely names a person, and the key is an internal storage location.
/// </para>
/// </remarks>
public sealed class ImportBatch
{
    public const int MaximumFileNameLength = 255;

    private ImportBatch() { }

    public ImportBatch(
        Guid id,
        string storageKey,
        string originalFileName,
        long sizeBytes,
        string sha256,
        Guid? createdByUserId,
        string? actorExternalKey,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new ArgumentException("A batch needs a storage key.", nameof(storageKey));
        }
        if (sizeBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        }
        if (sha256.Length != 64)
        {
            throw new ArgumentException("A batch records a SHA-256 hex digest.", nameof(sha256));
        }
        Id = id;
        StorageKey = storageKey;
        OriginalFileName = BoundFileName(originalFileName);
        SizeBytes = sizeBytes;
        Sha256 = sha256.ToLowerInvariant();
        CreatedByUserId = createdByUserId;
        ActorExternalKey = actorExternalKey;
        State = ImportBatchStates.Uploaded;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string State { get; private set; } = ImportBatchStates.Uploaded;
    public string StorageKey { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>
    /// The opaque correlation key of the actor who uploaded the batch. A commit runs in a worker
    /// with no request behind it, so this is what its candidate audit events carry.
    /// </summary>
    public string? ActorExternalKey { get; private set; }

    /// <summary>Data rows in the file, known once validation has read it.</summary>
    public int? RowCount { get; private set; }
    public int LoadedRows { get; private set; }
    public int RejectedRows { get; private set; }
    public int SkippedRows { get; private set; }

    /// <summary>A stable code: why the file was refused or why the run failed. Never a value.</summary>
    public string? FailureCode { get; private set; }

    /// <summary>
    /// Structural detail — a column name, a limit. It describes the file's shape and never
    /// carries a value from a data row.
    /// </summary>
    public string? FailureDetail { get; private set; }

    /// <summary>
    /// Catalog values that resolved to nothing, as JSON. Catalog vocabulary is not personal
    /// data, and it is reported precisely so an administrator can decide what it means.
    /// </summary>
    public string UnresolvedValuesJson { get; private set; } = "[]";

    /// <summary>
    /// Incremented each time recovery re-enqueues the batch's current step, so the new durable
    /// operation gets an idempotency key of its own rather than colliding with the dead one.
    /// </summary>
    public int OperationAttempt { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? ScannedAtUtc { get; private set; }
    public DateTimeOffset? ValidatedAtUtc { get; private set; }
    public DateTimeOffset? CommittedAtUtc { get; private set; }

    /// <summary>When the batch reached a closed state; the retention window counts from here.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public DateTimeOffset? FilePurgedAtUtc { get; private set; }

    public uint Version { get; private set; }

    public bool FileRetained => FilePurgedAtUtc is null;

    public int AccountedRows => LoadedRows + RejectedRows + SkippedRows;

    public void StartScan(DateTimeOffset now) => Move(ImportBatchStates.Uploaded, ImportBatchStates.Scanning, now);

    public void MarkScanned(DateTimeOffset now)
    {
        Move(ImportBatchStates.Scanning, ImportBatchStates.Scanned, now);
        ScannedAtUtc = now;
    }

    public void MarkInfected(string code, DateTimeOffset now) =>
        Refuse(ImportBatchStates.Scanning, ImportBatchStates.Infected, code, now);

    public void MarkUnscannable(string code, DateTimeOffset now) =>
        Refuse(ImportBatchStates.Scanning, ImportBatchStates.Unscannable, code, now);

    public void StartValidation(DateTimeOffset now) => Move(ImportBatchStates.Scanned, ImportBatchStates.Validating, now);

    public void CompleteValidation(
        int rowCount,
        int loadable,
        int rejected,
        int skipped,
        string unresolvedValuesJson,
        DateTimeOffset now)
    {
        RequireCounts(rowCount, loadable, rejected, skipped);
        Move(ImportBatchStates.Validating, ImportBatchStates.Validated, now);
        RowCount = rowCount;
        LoadedRows = loadable;
        RejectedRows = rejected;
        SkippedRows = skipped;
        UnresolvedValuesJson = unresolvedValuesJson;
        ValidatedAtUtc = now;
        ClosedAtUtc = now;
    }

    public void StartCommit(DateTimeOffset now)
    {
        Move(ImportBatchStates.Validated, ImportBatchStates.Committing, now);
        ClosedAtUtc = null;
    }

    public void CompleteCommit(int loaded, int rejected, int skipped, DateTimeOffset now)
    {
        RequireCounts(RowCount ?? 0, loaded, rejected, skipped);
        Move(ImportBatchStates.Committing, ImportBatchStates.Committed, now);
        LoadedRows = loaded;
        RejectedRows = rejected;
        SkippedRows = skipped;
        CommittedAtUtc = now;
        ClosedAtUtc = now;
    }

    /// <summary>
    /// A validation or commit that could not complete. Only reachable from the two running
    /// steps; a file refusal has its own terminal states.
    /// </summary>
    public void Fail(string code, string? detail, DateTimeOffset now)
    {
        if (State is not (ImportBatchStates.Validating or ImportBatchStates.Committing))
        {
            throw InvalidTransition(ImportBatchStates.Failed);
        }
        State = ImportBatchStates.Failed;
        FailureCode = RequireCode(code);
        FailureDetail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        UpdatedAtUtc = now;
        ClosedAtUtc = now;
    }

    /// <summary>
    /// Records that the uploaded file was removed. Counts, row outcomes and the failure code
    /// survive. A validated, committed or failed batch becomes <c>expired</c>, which is how it
    /// reports that its file is gone; a refused file keeps its refusal state, because
    /// "infected" is the more useful thing to say about it.
    /// </summary>
    public void MarkFilePurged(DateTimeOffset now)
    {
        if (!ImportBatchStates.Closed.Contains(State, StringComparer.Ordinal))
        {
            throw InvalidTransition(ImportBatchStates.Expired);
        }
        if (State is ImportBatchStates.Validated or ImportBatchStates.Committed or ImportBatchStates.Failed)
        {
            State = ImportBatchStates.Expired;
        }
        FilePurgedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void IncrementOperationAttempt(DateTimeOffset now)
    {
        OperationAttempt++;
        UpdatedAtUtc = now;
    }

    private void Move(string from, string to, DateTimeOffset now)
    {
        if (!string.Equals(State, from, StringComparison.Ordinal))
        {
            throw InvalidTransition(to);
        }
        State = to;
        UpdatedAtUtc = now;
    }

    private void Refuse(string from, string to, string code, DateTimeOffset now)
    {
        Move(from, to, now);
        FailureCode = RequireCode(code);
        ClosedAtUtc = now;
    }

    private static void RequireCounts(int rowCount, int loaded, int rejected, int skipped)
    {
        if (rowCount < 0 || loaded < 0 || rejected < 0 || skipped < 0 || loaded + rejected + skipped != rowCount)
        {
            throw new InvalidOperationException("Every data row must end in exactly one outcome.");
        }
    }

    private static string RequireCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 100)
        {
            throw new ArgumentException("A stable, bounded code is required.", nameof(code));
        }
        return code;
    }

    private InvalidOperationException InvalidTransition(string to) =>
        new($"import.batch.transition_invalid:{State}->{to}");

    private static string BoundFileName(string originalFileName)
    {
        var name = Path.GetFileName((originalFileName ?? string.Empty).Trim());
        if (name.Length == 0)
        {
            name = "import.csv";
        }
        return name.Length > MaximumFileNameLength ? name[..MaximumFileNameLength] : name;
    }
}
