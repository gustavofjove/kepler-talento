using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using KeplerTalento.Application.Abstractions.Documents;

namespace KeplerTalento.Infrastructure.Documents;

public sealed class ClamAvScanner(ClamAvOptions options) : IMalwareScanner
{
    private const int ChunkSize = 64 * 1024;

    public async Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(options.Host, options.Port, timeout.Token);
            await using var network = client.GetStream();
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            var buffer = new byte[ChunkSize];
            int read;
            long total = 0;
            while ((read = await content.ReadAsync(buffer, timeout.Token)) > 0)
            {
                total += read;
                if (total > DocumentStorageOptions.AbsoluteMaximumBytes) return new(ScanVerdict.Error, "scanner.size.exceeded");
                var length = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(length, read);
                await network.WriteAsync(length, timeout.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            await network.WriteAsync(new byte[4], timeout.Token);
            await network.FlushAsync(timeout.Token);
            var responseBuffer = new byte[4096];
            var responseLength = await network.ReadAsync(responseBuffer, timeout.Token);
            return ParseResponse(Encoding.UTF8.GetString(responseBuffer, 0, responseLength).TrimEnd('\0', '\r', '\n'));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ScanVerdict.Error, "scanner.timeout");
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ScanVerdict.Error, "scanner.unavailable");
        }
    }

    public static ScanResult ParseResponse(string response)
    {
        if (response.EndsWith(" OK", StringComparison.Ordinal)) return new(ScanVerdict.Clean, "scanner.clean");
        if (response.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var marker = response.LastIndexOf(':');
            var signature = marker >= 0 ? response[(marker + 1)..].Replace("FOUND", string.Empty, StringComparison.Ordinal).Trim() : null;
            return new(ScanVerdict.Infected, "scanner.infected", signature);
        }
        return new(ScanVerdict.Error, "scanner.error");
    }
}
