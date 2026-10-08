using System.Net;
using System.Net.Sockets;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Infrastructure;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Web.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Documents;

public sealed class ScannerBypassTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ktl-bypass-{Guid.NewGuid():N}");

    [Fact]
    public async Task Bypass_scanner_marks_content_clean_under_its_own_code()
    {
        var result = await new BypassMalwareScanner().ScanAsync(new MemoryStream("synthetic"u8.ToArray()), CancellationToken.None);

        Assert.Equal(ScanVerdict.Clean, result.Verdict);
        Assert.Equal("scanner.bypassed", result.Code);
        Assert.Equal("scanner-bypass", result.Signature);
    }

    [Theory]
    [InlineData(null, typeof(ClamAvScanner))]
    [InlineData("false", typeof(ClamAvScanner))]
    [InlineData("true", typeof(BypassMalwareScanner))]
    public void Infrastructure_registers_the_bypass_only_when_it_is_switched_on(string? bypass, Type expected)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ApplicationDatabase"] = "Host=localhost;Database=unused",
            ["DocumentStorage:Root"] = _root,
        };
        if (bypass is not null) settings["ClamAv:Bypass"] = bypass;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection().AddInfrastructure(configuration);

        var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IMalwareScanner));
        Assert.Equal(expected, registration.ImplementationType);
    }

    [Fact]
    public async Task Scanner_health_stays_degraded_while_bypassed_even_when_clamav_answers()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var reachable = new ClamAvOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 1 };
            var bypassed = new ClamAvOptions { Host = "127.0.0.1", Port = port, TimeoutSeconds = 1, Bypass = true };

            var scanning = await new ScannerHealthCheck(reachable).CheckHealthAsync(new HealthCheckContext());
            var skipping = await new ScannerHealthCheck(bypassed).CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, scanning.Status);
            Assert.Equal(HealthStatus.Degraded, skipping.Status);
            Assert.Equal("scanner_bypassed", skipping.Description);
        }
        finally
        {
            listener.Stop();
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
