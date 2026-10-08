using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

[Collection(WebHostCollection.Name)]
public sealed class ScannerBypassTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public void Scanner_bypass_cannot_start_in_production()
    {
        // Other suites leave the development actor or issuer switched on in the process
        // environment, and their own Production guards would fire first. Switch both off so the
        // failure can only come from the scanner bypass.
        string[] names = ["ClamAv__Bypass", "DevelopmentActor__Enabled", "Authentication__DevelopmentIssuer__Enabled"];
        var previous = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
            Environment.SetEnvironmentVariable("ClamAv__Bypass", "true");
            Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false");
            Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "false");

            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseEnvironment("Production"));

            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("ClamAv:Bypass cannot be enabled in Production", exception.ToString());
        }
        finally
        {
            foreach (var (name, value) in previous) Environment.SetEnvironmentVariable(name, value);
        }
    }
}
