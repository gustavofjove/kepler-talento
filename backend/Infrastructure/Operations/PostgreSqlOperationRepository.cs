using System.Data;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KeplerTalento.Infrastructure.Operations;

public sealed class PostgreSqlOperationRepository(ApplicationDbContext dbContext) : IOperationRepository
{
    public async Task<Operation> EnqueueAsync(string type, string correlationId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Operations.SingleOrDefaultAsync(value => value.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existing is not null) return existing;
        var operation = new Operation(Guid.NewGuid(), type, correlationId, idempotencyKey, DateTimeOffset.UtcNow);
        dbContext.Operations.Add(operation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return operation;
    }

    public async Task<Operation?> ClaimNextAsync(string owner, TimeSpan lease, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"OPS_Operations\" SET \"Status\" = 'Failed', \"OutcomeCode\" = 'operation.retry.exhausted', \"Owner\" = NULL, \"LeaseExpiresAtUtc\" = NULL, \"UpdatedAtUtc\" = now() WHERE \"Status\" = 'Running' AND \"LeaseExpiresAtUtc\" <= now() AND \"AttemptCount\" >= \"MaxAttempts\"",
            cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var operation = await dbContext.Operations
            .FromSqlRaw("SELECT operation.*, operation.xmin FROM \"OPS_Operations\" AS operation WHERE (operation.\"Status\" = 'Queued' OR (operation.\"Status\" = 'Running' AND operation.\"LeaseExpiresAtUtc\" <= now())) AND operation.\"AttemptCount\" < operation.\"MaxAttempts\" ORDER BY operation.\"CreatedAtUtc\" FOR UPDATE SKIP LOCKED LIMIT 1")
            .SingleOrDefaultAsync(cancellationToken);
        if (operation is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!operation.TryClaim(owner, DateTimeOffset.UtcNow, lease)) return null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return operation;
    }

    public async Task<bool> RenewLeaseAsync(Guid id, string owner, TimeSpan lease, CancellationToken cancellationToken)
    {
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"OPS_Operations\" SET \"LeaseExpiresAtUtc\" = {DateTimeOffset.UtcNow.Add(lease)}, \"UpdatedAtUtc\" = {DateTimeOffset.UtcNow} WHERE \"Id\" = {id} AND \"Status\" = 'Running' AND \"Owner\" = {owner}",
            cancellationToken);
        if (affected == 1)
        {
            var tracked = dbContext.ChangeTracker.Entries<Operation>().SingleOrDefault(entry => entry.Entity.Id == id);
            if (tracked is not null) await tracked.ReloadAsync(cancellationToken);
        }
        return affected == 1;
    }

    public async Task<bool> CompleteAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken)
    {
        var operation = await dbContext.Operations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (operation is null || !operation.Complete(owner, outcomeCode, DateTimeOffset.UtcNow)) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> FailAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken)
    {
        var operation = await dbContext.Operations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (operation is null || !operation.Fail(owner, outcomeCode, DateTimeOffset.UtcNow)) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelAsync(Guid id, string? owner, string outcomeCode, CancellationToken cancellationToken)
    {
        var operation = await dbContext.Operations.SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        if (operation is null || !operation.Cancel(owner, outcomeCode, DateTimeOffset.UtcNow)) return false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<Operation?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Operations.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Operation>> FindByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken) =>
        await dbContext.Operations
            .AsNoTracking()
            .Where(value => value.CorrelationId == correlationId)
            .OrderByDescending(value => value.CreatedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);
}
