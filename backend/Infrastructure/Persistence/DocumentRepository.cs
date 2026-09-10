using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class DocumentRepository(ApplicationDbContext dbContext) : IDocumentRepository
{
    public Task<bool> CandidateExistsAsync(Guid candidateId, CancellationToken cancellationToken) =>
        dbContext.Candidates.AnyAsync(candidate => candidate.Id == candidateId, cancellationToken);

    public Task<CandidateDocument?> FindAsync(
        Guid candidateId,
        Guid documentId,
        CancellationToken cancellationToken) =>
        dbContext.Documents.AsNoTracking().SingleOrDefaultAsync(
            document => document.CandidateId == candidateId && document.Id == documentId,
            cancellationToken);

    public async Task<IReadOnlyList<CandidateDocument>> ListAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        await dbContext.Documents.AsNoTracking()
            .Where(document => document.CandidateId == candidateId)
            .OrderByDescending(document => document.IsPrimary)
            .ThenByDescending(document => document.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task ClearPrimaryAsync(
        Guid candidateId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var primary = await dbContext.Documents.SingleOrDefaultAsync(
            document => document.CandidateId == candidateId && document.IsPrimary,
            cancellationToken);
        primary?.SetPrimary(false, now);
    }

    public void Add(CandidateDocument document) => dbContext.Documents.Add(document);

    public void AddAudit(
        string eventType,
        Guid candidateId,
        Guid documentId,
        string outcome,
        string correlationId,
        string? actorExternalKey) =>
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(),
            eventType,
            Subject(candidateId, documentId),
            correlationId,
            DateTimeOffset.UtcNow,
            Outcome(outcome, actorExternalKey)));

    public async Task<DocumentSaveOutcome> SetPrimaryAsync(
        Guid candidateId,
        Guid documentId,
        string correlationId,
        string? actorExternalKey,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var documents = await dbContext.Documents
                .Where(document => document.CandidateId == candidateId)
                .ToListAsync(cancellationToken);
            var selected = documents.SingleOrDefault(document => document.Id == documentId);
            if (selected is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DocumentSaveOutcome.NotFound;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var document in documents.Where(document => document.IsPrimary && document.Id != documentId))
            {
                document.SetPrimary(false, now);
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            selected.SetPrimary(true, now);
            AddAudit("document.primary.changed", candidateId, documentId, "applied", correlationId, actorExternalKey);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DocumentSaveOutcome.Saved;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(cancellationToken);
            return DocumentSaveOutcome.Conflict;
        }
    }

    public async Task<CandidateDocument?> RemoveAsync(
        Guid candidateId,
        Guid documentId,
        string correlationId,
        string? actorExternalKey,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.Documents.SingleOrDefaultAsync(
            value => value.CandidateId == candidateId && value.Id == documentId,
            cancellationToken);
        if (document is null) return null;

        dbContext.Documents.Remove(document);
        AddAudit("document.removed", candidateId, documentId, "applied", correlationId, actorExternalKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);

    private static string Subject(Guid candidateId, Guid documentId) =>
        $"candidate:{candidateId:N};document:{documentId:N}";

    private static string Outcome(string outcome, string? actorExternalKey)
    {
        var actor = string.IsNullOrWhiteSpace(actorExternalKey) ? "system" : actorExternalKey.Trim();
        var value = $"{outcome}|actor:{actor}";
        return value[..Math.Min(value.Length, 100)];
    }
}
