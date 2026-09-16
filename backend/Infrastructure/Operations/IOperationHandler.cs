using KeplerTalento.Domain.Operations;
using KeplerTalento.Infrastructure.Documents;

namespace KeplerTalento.Infrastructure.Operations;

/// <summary>
/// Executes one durable operation type. The worker claims an operation, finds the handler whose
/// <see cref="Type"/> matches, and records the outcome; an operation with no handler fails with
/// a stable code rather than being retried forever.
/// </summary>
/// <remarks>
/// A handler must be idempotent: a claim can be retried after its lease expires, including after
/// a restart, so executing the same operation twice must leave the same result as once.
/// </remarks>
public interface IOperationHandler
{
    string Type { get; }

    Task<ScanOperationOutcome> HandleAsync(Operation operation, CancellationToken cancellationToken);
}
