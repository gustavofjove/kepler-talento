using System.Text;
using DotNet.Testcontainers.Builders;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Infrastructure.Documents;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class ClamAvContractTests
{
    [Fact]
    [Trait("Category", "ClamAV")]
    public async Task Stream_protocol_detects_eicar_and_accepts_clean_synthetic_text()
    {
        await using var container = new ContainerBuilder("clamav/clamav:1.4.3")
            .WithPortBinding(3310, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(3310))
            .Build();
        await container.StartAsync();
        var scanner = new ClamAvScanner(new ClamAvOptions
        {
            Host = container.Hostname,
            Port = container.GetMappedPublicPort(3310),
            TimeoutSeconds = 120,
        });

        var clean = await scanner.ScanAsync(new MemoryStream(Encoding.UTF8.GetBytes("synthetic curriculum")), CancellationToken.None);
        const string eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        var infected = await scanner.ScanAsync(new MemoryStream(Encoding.ASCII.GetBytes(eicar)), CancellationToken.None);

        Assert.Equal(ScanVerdict.Clean, clean.Verdict);
        Assert.Equal(ScanVerdict.Infected, infected.Verdict);
        Assert.Contains("Eicar", infected.Signature, StringComparison.OrdinalIgnoreCase);
    }
}
