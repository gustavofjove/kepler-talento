using KeplerTalento.Domain.Operations;

namespace KeplerTalento.Application.Abstractions.Operations;

public interface IOperationRepository
{
    Task<Operation> EnqueueAsync(string type, string correlationId, string idempotencyKey, CancellationToken cancellationToken);
    Task<Operation?> ClaimNextAsync(string owner, TimeSpan lease, CancellationToken cancellationToken);
    Task<bool> RenewLeaseAsync(Guid id, string owner, TimeSpan lease, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken);
    Task<bool> FailAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid id, string? owner, string outcomeCode, CancellationToken cancellationToken);
    Task<Operation?> FindAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Operation>> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
}
