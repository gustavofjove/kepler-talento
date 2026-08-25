namespace KeplerTalento.Infrastructure.Operations;

public sealed class OperationWorkerOptions
{
    public const string SectionName = "OperationWorker";
    public bool Enabled { get; init; }
    public int PollSeconds { get; init; } = 5;
    public int LeaseSeconds { get; init; } = 60;
}
