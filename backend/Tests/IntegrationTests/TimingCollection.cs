using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Tests that assert on elapsed time. xUnit runs this collection on its own, after the parallel
/// ones, so a measurement never competes with other test classes for the machine.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingCollection
{
    public const string Name = "Timing";
}
