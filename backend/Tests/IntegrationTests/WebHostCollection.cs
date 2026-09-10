using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// Test classes that boot the API through <c>WebApplicationFactory</c>.
/// </summary>
/// <remarks>
/// They must not run concurrently with each other. The host resolves its connection
/// string while registering infrastructure, before the factory's configuration overrides
/// apply, so each of these classes points the API at its own disposable database through
/// a <em>process-wide</em> environment variable. Two of them running at once would race,
/// and one would silently talk to the other's database.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WebHostCollection
{
    public const string Name = "web-host";
}
