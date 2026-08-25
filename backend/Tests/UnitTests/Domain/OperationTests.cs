using KeplerTalento.Domain.Operations;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Domain;

public sealed class OperationTests
{
    [Fact]
    public void Only_one_active_owner_can_claim()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new Operation(Guid.NewGuid(), "document.scan", "corr", "key", now);
        Assert.True(operation.TryClaim("worker-a", now, TimeSpan.FromMinutes(1)));
        Assert.False(operation.TryClaim("worker-b", now.AddSeconds(1), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void Completion_is_idempotent_and_requires_owner()
    {
        var now = DateTimeOffset.UtcNow;
        var operation = new Operation(Guid.NewGuid(), "document.scan", "corr", "key", now);
        operation.TryClaim("worker-a", now, TimeSpan.FromMinutes(1));
        Assert.False(operation.Complete("worker-b", "clean", now));
        Assert.True(operation.Complete("worker-a", "clean", now));
        Assert.True(operation.Complete("worker-a", "clean", now));
    }

    [Fact]
    public void Cancellation_is_idempotent_and_a_running_operation_requires_its_owner()
    {
        var now = DateTimeOffset.UtcNow;
        var queued = new Operation(Guid.NewGuid(), "export.csv", "corr", "queued", now);
        Assert.True(queued.Cancel(null, "operation.cancelled", now));
        Assert.True(queued.Cancel(null, "operation.cancelled", now));

        var running = new Operation(Guid.NewGuid(), "document.scan", "corr", "running", now);
        running.TryClaim("worker-a", now, TimeSpan.FromMinutes(1));
        Assert.False(running.Cancel("worker-b", "operation.cancelled", now));
        Assert.True(running.Cancel("worker-a", "operation.cancelled", now));
        Assert.False(running.Complete("worker-a", "scanner.clean", now));
    }
}
