using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Tools.DataMigration.Export;
using KeplerTalento.Tools.DataMigration.Validation;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Tools.DataMigration.Loading;

/// <summary>
/// Moves the CV binaries the manifest names into private storage.
/// </summary>
/// <remarks>
/// <para>
/// Migrated content is not trusted content. Every file passes the same gates an interactive
/// upload does — allowlist, size, quarantine, scan — and reaches available storage only on a
/// clean result. Its origin buys it nothing.
/// </para>
/// <para>
/// A document failing does not reject its candidate: a document is content attached to a
/// person, not a field of them, and losing the person because one CV file is corrupt would
/// protect less than it destroys. The document fails closed on its own and is reported.
/// </para>
/// </remarks>
public sealed class DocumentMigrator(
    Func<ApplicationDbContext> contextFactory,
    IDocumentStorage storage,
    IDocumentContentInspector inspector,
    IMalwareScanner scanner,
    long maximumBytes)
{
    /// <summary>
    /// Verifies every manifest document without writing anything: the file is there, its
    /// format is allowed, its bytes hash to what the manifest declared, and the scanner is
    /// content with it.
    /// </summary>
    /// <remarks>
    /// This is what <c>validate</c> runs. A hash mismatch or an infected CV is something an
    /// operator wants to know about before committing, not after, and none of those checks
    /// needs the binary to be in private storage first.
    /// </remarks>
    public async Task VerifyAsync(
        ExportSet exportSet,
        IReadOnlySet<string> loadableCandidateKeys,
        LoadResult result,
        CancellationToken cancellationToken)
    {
        foreach (var row in exportSet[ExportContract.Documents].Rows)
        {
            var sourceKey = row[ExportContract.SourceKeyColumn].Trim();
            var candidateKey = row[ExportContract.CandidateSourceKeyColumn].Trim();
            void Reject(string field, string reason) =>
                result.AddProblem(new RowProblem(MigrationEntities.Document, sourceKey, field, reason));

            if (!loadableCandidateKeys.Contains(candidateKey))
            {
                Reject(ExportContract.CandidateSourceKeyColumn, ReasonCodes.CandidateRejected);
                continue;
            }

            var sourcePath = ResolveSourcePath(exportSet.FilesDirectory, row["RelativePath"].Trim());
            if (sourcePath is null || !File.Exists(sourcePath))
            {
                Reject("RelativePath", ReasonCodes.DocumentMissingFile);
                continue;
            }

            await using (var content = File.OpenRead(sourcePath))
            {
                var inspected = await inspector.InspectAsync(
                    row["OriginalFileName"].Trim(), content, cancellationToken);
                if (!inspected.Accepted)
                {
                    Reject("RelativePath", ReasonCodes.DocumentFormatNotAllowed);
                    continue;
                }
            }

            var size = new FileInfo(sourcePath).Length;
            if (size > maximumBytes)
            {
                Reject("RelativePath", ReasonCodes.DocumentTooLarge);
                continue;
            }

            string actualHash;
            await using (var content = File.OpenRead(sourcePath))
            {
                actualHash = Convert
                    .ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(content, cancellationToken))
                    .ToLowerInvariant();
            }
            if (!string.Equals(actualHash, row["Sha256"].Trim().ToLowerInvariant(), StringComparison.Ordinal))
            {
                Reject("Sha256", ReasonCodes.DocumentHashMismatch);
                continue;
            }

            await using (var content = File.OpenRead(sourcePath))
            {
                var scan = await scanner.ScanAsync(content, cancellationToken);
                if (scan.Verdict == ScanVerdict.Infected)
                {
                    Reject("RelativePath", ReasonCodes.DocumentRejectedByScanner);
                    continue;
                }
                if (scan.Verdict != ScanVerdict.Clean)
                {
                    Reject("RelativePath", ReasonCodes.DocumentUnscannable);
                    continue;
                }
            }

            result.Record(MigrationEntities.Document, sourceKey, RowOutcome.Loaded);
        }
    }

    public async Task MigrateAsync(
        ExportSet exportSet,
        IReadOnlyDictionary<string, Guid> candidateIdsBySourceKey,
        LoadResult result,
        CancellationToken cancellationToken)
    {
        foreach (var row in exportSet[ExportContract.Documents].Rows)
        {
            var sourceKey = row[ExportContract.SourceKeyColumn].Trim();
            var candidateKey = row[ExportContract.CandidateSourceKeyColumn].Trim();

            if (!candidateIdsBySourceKey.TryGetValue(candidateKey, out var candidateId))
            {
                // Its candidate was rejected or skipped; the document has nowhere to attach.
                result.AddProblem(new RowProblem(
                    MigrationEntities.Document,
                    sourceKey,
                    ExportContract.CandidateSourceKeyColumn,
                    ReasonCodes.CandidateRejected));
                continue;
            }

            var outcome = await MigrateOneAsync(
                exportSet.FilesDirectory, row, sourceKey, candidateId, result, cancellationToken);
            if (outcome == RowOutcome.Loaded)
            {
                result.Record(MigrationEntities.Document, sourceKey, RowOutcome.Loaded);
            }
        }
    }

    private async Task<RowOutcome> MigrateOneAsync(
        string filesDirectory,
        CsvRow row,
        string sourceKey,
        Guid candidateId,
        LoadResult result,
        CancellationToken cancellationToken)
    {
        void Reject(string field, string reason) =>
            result.AddProblem(new RowProblem(MigrationEntities.Document, sourceKey, field, reason));

        var declaredHash = row["Sha256"].Trim().ToLowerInvariant();
        var relativePath = row["RelativePath"].Trim();
        var sourcePath = ResolveSourcePath(filesDirectory, relativePath);
        if (sourcePath is null || !File.Exists(sourcePath))
        {
            Reject("RelativePath", ReasonCodes.DocumentMissingFile);
            return RowOutcome.Rejected;
        }

        await using var dbContext = contextFactory();
        var document = await dbContext.Documents
            .SingleOrDefaultAsync(value => value.SourceKey == sourceKey, cancellationToken);

        // A re-run over an unchanged document does no file work: the binary is already
        // stored, clean, and provably the same one.
        if (document is not null
            && document.ScanState == DocumentScanState.Clean
            && string.Equals(document.Sha256, declaredHash, StringComparison.Ordinal)
            && await storage.AvailableExistsAsync(document.StorageKey, cancellationToken))
        {
            ApplyMetadata(document, row);
            await dbContext.SaveChangesAsync(cancellationToken);
            return RowOutcome.Loaded;
        }

        var documentId = document?.Id ?? Guid.CreateVersion7();
        var storageKey = document?.StorageKey ?? DocumentStorageKey.Create(candidateId, documentId);

        var originalFileName = row["OriginalFileName"].Trim();
        await using (var content = File.OpenRead(sourcePath))
        {
            var inspected = await inspector.InspectAsync(originalFileName, content, cancellationToken);
            if (!inspected.Accepted)
            {
                Reject("RelativePath", ReasonCodes.DocumentFormatNotAllowed);
                return RowOutcome.Rejected;
            }
        }

        // A previous attempt may have left the key occupied.
        await storage.DeleteQuarantineIfExistsAsync(storageKey, cancellationToken);

        StoredFile stored;
        try
        {
            await using var content = File.OpenRead(sourcePath);
            stored = await storage.WriteQuarantineAsync(storageKey, content, maximumBytes, cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Message == "document.size.exceeded")
        {
            Reject("RelativePath", ReasonCodes.DocumentTooLarge);
            return RowOutcome.Rejected;
        }
        catch (InvalidOperationException)
        {
            Reject("RelativePath", ReasonCodes.DocumentNotStored);
            return RowOutcome.Rejected;
        }

        // The hash is what proves the binary now in private storage is the binary Access
        // held. A mismatch means the manifest and the file disagree, and which of them is
        // wrong is not the migration's to guess.
        if (!string.Equals(stored.Sha256, declaredHash, StringComparison.Ordinal))
        {
            await storage.DeleteQuarantineIfExistsAsync(storageKey, cancellationToken);
            Reject("Sha256", ReasonCodes.DocumentHashMismatch);
            return RowOutcome.Rejected;
        }

        document = await UpsertRowAsync(dbContext, document, documentId, candidateId, storageKey, stored, row, cancellationToken);

        ScanResult scan;
        try
        {
            await using var quarantined = await storage.OpenQuarantineAsync(storageKey, cancellationToken);
            scan = await scanner.ScanAsync(quarantined, cancellationToken);
        }
        catch (IOException)
        {
            scan = new ScanResult(ScanVerdict.Error, "scanner.unavailable");
        }

        var scannedAtUtc = DateTimeOffset.UtcNow;
        switch (scan.Verdict)
        {
            case ScanVerdict.Clean:
                await storage.PromoteAsync(storageKey, cancellationToken);
                document.MarkClean(scan.Signature, scannedAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
                return RowOutcome.Loaded;

            case ScanVerdict.Infected:
                document.MarkUnavailable(DocumentScanState.Infected, scan.Code, scannedAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
                await storage.DeleteQuarantineIfExistsAsync(storageKey, cancellationToken);
                Reject("RelativePath", ReasonCodes.DocumentRejectedByScanner);
                return RowOutcome.Rejected;

            default:
                // Fails closed: unscannable content stays unavailable and is reported for
                // operator review rather than being let through.
                document.MarkUnavailable(DocumentScanState.ScanFailed, scan.Code, scannedAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
                Reject("RelativePath", ReasonCodes.DocumentUnscannable);
                return RowOutcome.Rejected;
        }
    }

    private static async Task<CandidateDocument> UpsertRowAsync(
        ApplicationDbContext dbContext,
        CandidateDocument? document,
        Guid documentId,
        Guid candidateId,
        string storageKey,
        StoredFile stored,
        CsvRow row,
        CancellationToken cancellationToken)
    {
        if (document is null)
        {
            document = new CandidateDocument(
                documentId,
                candidateId,
                storageKey,
                row["OriginalFileName"].Trim(),
                row["ContentType"].Trim(),
                stored.Size,
                stored.Sha256,
                DateTimeOffset.UtcNow);
            document.SetSourceKey(row[ExportContract.SourceKeyColumn].Trim());
            dbContext.Documents.Add(document);
        }
        ApplyMetadata(document, row);
        await dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    private static void ApplyMetadata(CandidateDocument document, CsvRow row)
    {
        document.SetDocumentType(row["DocumentType"]);
        FieldParsers.TryBoolean(row["IsPrimary"], out var isPrimary);
        document.SetPrimary(isPrimary, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Resolves a manifest path inside the export's files directory. Validation has already
    /// rejected traversal, so this is the second of two checks rather than the only one.
    /// </summary>
    private static string? ResolveSourcePath(string filesDirectory, string relativePath)
    {
        if (!FieldParsers.IsSafeRelativePath(relativePath))
        {
            return null;
        }
        var root = Path.GetFullPath(filesDirectory);
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return resolved.StartsWith(prefix, StringComparison.Ordinal) ? resolved : null;
    }
}
