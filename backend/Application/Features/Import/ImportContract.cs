using System.Text.Json;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Import.Rows;
using KeplerTalento.Domain.Import;

namespace KeplerTalento.Application.Features.Import;

/// <summary>
/// An import batch as a caller sees it.
/// </summary>
/// <remarks>
/// There is deliberately no storage key or path. <see cref="OriginalFileName"/> is returned only
/// to the actor who uploaded the batch: a filename routinely names a person, and the spec allows
/// showing it back to its uploader and nobody else. <see cref="Sha256"/> is a digest, not
/// content, and is what lets the page warn about a deliberate re-import (design D4).
/// </remarks>
public sealed record ImportBatchResponse(
    Guid Id,
    string State,
    string? OriginalFileName,
    long SizeBytes,
    string Sha256,
    int? RowCount,
    int LoadedRows,
    int RejectedRows,
    int SkippedRows,
    string? FailureCode,
    string? FailureDetail,
    IReadOnlyList<UnresolvedValue> UnresolvedValues,
    bool FileRetained,
    bool UploadedByCaller,
    DateTimeOffset? SameFileCommittedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? CommittedAt,
    uint Version)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ImportBatchResponse From(ImportBatch batch, ICurrentActor actor, DateTimeOffset? sameFileCommittedAt)
    {
        var uploadedByCaller = IsUploader(batch, actor);
        return new(
            batch.Id,
            batch.State,
            uploadedByCaller ? batch.OriginalFileName : null,
            batch.SizeBytes,
            batch.Sha256,
            batch.RowCount,
            batch.LoadedRows,
            batch.RejectedRows,
            batch.SkippedRows,
            batch.FailureCode,
            batch.FailureDetail,
            JsonSerializer.Deserialize<List<UnresolvedValue>>(batch.UnresolvedValuesJson, Json) ?? [],
            batch.FileRetained,
            uploadedByCaller,
            sameFileCommittedAt,
            batch.CreatedAtUtc,
            batch.UpdatedAtUtc,
            batch.ValidatedAtUtc,
            batch.CommittedAtUtc,
            batch.Version);
    }

    public static string SerializeUnresolved(IReadOnlyList<UnresolvedValue> values) =>
        JsonSerializer.Serialize(values, Json);

    private static bool IsUploader(ImportBatch batch, ICurrentActor actor)
    {
        if (batch.CreatedByUserId is not null)
        {
            return batch.CreatedByUserId == actor.UserId;
        }
        return batch.ActorExternalKey is not null
            && string.Equals(batch.ActorExternalKey, actor.ExternalKey, StringComparison.Ordinal);
    }
}

public sealed record ImportBatchPageResponse(
    IReadOnlyList<ImportBatchResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>
/// One row's outcome. A row number, a column name and a reason code — and nothing else. There
/// is no value field, and no candidate id: holding <c>candidates.import</c> does not confer the
/// right to read candidates.
/// </summary>
public sealed record ImportRowOutcomeResponse(
    int RowNumber,
    string Outcome,
    string? Field,
    string? ReasonCode);

public sealed record ImportRowReportResponse(
    Guid BatchId,
    string Phase,
    IReadOnlyList<ImportRowOutcomeResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>Durable operation types and their idempotency keys.</summary>
public static class ImportOperations
{
    public const string Scan = "import.scan";
    public const string Validate = "import.validate";
    public const string Commit = "import.commit";
    public const string Purge = "import.purge";

    public static string Key(string type, Guid batchId, int attempt) =>
        $"{type}:{batchId:N}:{attempt}";

    public static bool TryParseKey(string type, string idempotencyKey, out Guid batchId)
    {
        batchId = Guid.Empty;
        var prefix = type + ":";
        if (!idempotencyKey.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }
        var segments = idempotencyKey[prefix.Length..].Split(':');
        return segments.Length == 2
            && int.TryParse(segments[1], out _)
            && Guid.TryParseExact(segments[0], "N", out batchId);
    }
}
