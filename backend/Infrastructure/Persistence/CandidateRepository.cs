using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KeplerTalento.Infrastructure.Persistence;

public sealed class CandidateRepository(
    ApplicationDbContext dbContext,
    ICorrelationContext correlation,
    ICurrentActor actor) : ICandidateRepository
{
    public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        // No IsActive filter: a logically removed candidate must stay retrievable by
        // identifier, and restorable.
        dbContext.Candidates
            .Include(candidate => candidate.Languages)
            .Include(candidate => candidate.Programs)
            .Include(candidate => candidate.Education)
            .Include(candidate => candidate.Experience)
            .Include(candidate => candidate.Skills)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    public Task<Candidate?> FindCoreAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Candidates.SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CandidateSummary>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Candidates.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(candidate => candidate.IsActive);
        }
        return await query
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .Select(candidate => new CandidateSummary(
                candidate.Id,
                candidate.FirstName,
                candidate.LastName,
                candidate.Phone,
                candidate.Email,
                candidate.Location,
                candidate.Province,
                candidate.Country,
                candidate.Availability,
                candidate.Status,
                candidate.Source,
                candidate.Notes,
                candidate.ReceivedAt,
                candidate.ConsentAt,
                candidate.ReviewDueAt,
                candidate.IsActive,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc,
                candidate.Version,
                dbContext.Documents.Count(document => document.CandidateId == candidate.Id),
                dbContext.Documents
                    .Where(document => document.CandidateId == candidate.Id && document.IsPrimary)
                    .Select(document => (Guid?)document.Id)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateDocument>> ListDocumentsAsync(
        Guid candidateId,
        CancellationToken cancellationToken) =>
        await dbContext.Documents
            .Where(document => document.CandidateId == candidateId)
            .OrderByDescending(document => document.IsPrimary)
            .ThenBy(document => document.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<SearchPage<CandidateSearchItem>> SearchAsync(
        SearchFiltersValue filters,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var search = new CandidateSearchQuery(dbContext);
        var matching = await search.MatchingAsync(filters, cancellationToken);
        var totalCount = await matching.CountAsync(cancellationToken);
        var items = await search.Page(matching, page, pageSize).ToListAsync(cancellationToken);
        return new SearchPage<CandidateSearchItem>(items, page, pageSize, totalCount);
    }

    public void Add(Candidate candidate) => dbContext.Candidates.Add(candidate);

    public void AddRelation(CandidateRelation relation) => dbContext.Add(relation);

    public void RemoveRelations(IEnumerable<CandidateRelation> relations)
    {
        foreach (var relation in relations)
        {
            dbContext.Remove(relation);
        }
    }

    public async Task<CandidateSaveOutcome> ReplaceDocumentsAsync(
        Candidate candidate,
        uint version,
        IReadOnlyList<DocumentMetadata> desired,
        string auditEventType,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Documents
            .Where(document => document.CandidateId == candidate.Id)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Phase one: everything that can only free the primary slot — the documents
            // leaving the collection, and the ones that stop being primary. Committing
            // this first is what keeps the partial unique index satisfied at every
            // instant, since it is not deferrable and EF does not order its updates.
            var keptIds = desired.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();
            dbContext.Documents.RemoveRange(existing.Where(document => !keptIds.Contains(document.Id)));
            var stillPrimary = desired.Where(item => item.IsPrimary && item.Id is not null)
                .Select(item => item.Id!.Value)
                .ToHashSet();
            foreach (var document in existing.Where(document =>
                keptIds.Contains(document.Id) && document.IsPrimary && !stillPrimary.Contains(document.Id)))
            {
                document.SetPrimary(false, now);
            }
            await dbContext.SaveChangesAsync(cancellationToken);

            // Phase two: the additions, the surviving records' metadata, and the new
            // primary — now that the slot is provably free.
            foreach (var item in desired)
            {
                var document = item.Id is null
                    ? null
                    : existing.FirstOrDefault(candidateDocument => candidateDocument.Id == item.Id.Value);
                if (document is null)
                {
                    var documentId = Guid.CreateVersion7();
                    document = new CandidateDocument(
                        documentId,
                        candidate.Id,
                        DocumentStorageKey.Create(candidate.Id, documentId),
                        item.OriginalFilename,
                        item.MimeType,
                        item.SizeBytes,
                        // The bytes and their digest arrive with KTL-9. Until then a
                        // document record is metadata, and the digest column is empty
                        // rather than carrying a fabricated value.
                        sha256: string.Empty,
                        createdAtUtc: now);
                    dbContext.Documents.Add(document);
                }
                document.SetDocumentType(item.DocumentType);
                document.SetPrimary(item.IsPrimary, now);
            }

            // The candidate's own version is the token for everything it owns, so a
            // document change advances it exactly as a field change does.
            dbContext.Entry(candidate).Property(entity => entity.Version).OriginalValue = version;
            candidate.TouchUpdated(now);
            dbContext.AuditEvents.Add(new AuditEvent(
                Guid.CreateVersion7(),
                auditEventType,
                candidate.Id.ToString("N"),
                correlation.CorrelationId,
                now,
                actor.ExternalKey));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await RefreshVersionsAsync(cancellationToken);
            return CandidateSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CandidateSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.CheckViolation
                or PostgresErrorCodes.ForeignKeyViolation)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CandidateSaveOutcome.ConstraintViolation;
        }
    }

    public void ExpectVersion(Candidate candidate, uint version) =>
        dbContext.Entry(candidate).Property(entity => entity.Version).OriginalValue = version;

    /// <summary>
    /// Re-reads the concurrency token of every candidate this unit of work wrote.
    /// </summary>
    /// <remarks>
    /// The row version is PostgreSQL's <c>xmin</c>, a system column the write itself does
    /// not bring back, so a saved entity still carries the token it was read with. Every
    /// write answers with the whole aggregate and the browser cache uses that response's
    /// version for its next write — so without this refresh the second write of any pair
    /// would be refused as a conflict with itself.
    /// </remarks>
    private async Task RefreshVersionsAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries<Candidate>().ToList())
        {
            await entry.ReloadAsync(cancellationToken);
        }
    }

    public async Task<CandidateSaveOutcome> SaveAsync(
        string auditEventType,
        string subjectId,
        CancellationToken cancellationToken)
    {
        dbContext.AuditEvents.Add(new AuditEvent(
            Guid.CreateVersion7(),
            auditEventType,
            subjectId,
            correlation.CorrelationId,
            DateTimeOffset.UtcNow,
            actor.ExternalKey));
        try
        {
            // One SaveChangesAsync means one transaction: the aggregate change and its
            // audit event are stored together, or neither is.
            await dbContext.SaveChangesAsync(cancellationToken);
            await RefreshVersionsAsync(cancellationToken);
            return CandidateSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return CandidateSaveOutcome.ConcurrencyConflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres
            && postgres.SqlState is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.CheckViolation
                or PostgresErrorCodes.ForeignKeyViolation)
        {
            return CandidateSaveOutcome.ConstraintViolation;
        }
    }
}
