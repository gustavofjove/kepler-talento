using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Import;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Features.Import;
using KeplerTalento.Infrastructure.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>Shared pieces of the KTL-17 import integration tests.</summary>
internal static class ImportTestSupport
{
    public const string Header = "first_name,last_name,email,phone,status,consent_at,languages";

    /// <summary>
    /// Runs every queued durable operation the way the worker would — claim, handle, record —
    /// until none is left. The hosted worker is disabled in these tests so that the order of
    /// events is the test's to decide.
    /// </summary>
    public static async Task<int> RunOperationsAsync(IServiceProvider services, int maximum = 50)
    {
        var executed = 0;
        while (executed < maximum)
        {
            await using var scope = services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
            var operation = await repository.ClaimNextAsync("integration-test", TimeSpan.FromMinutes(5), CancellationToken.None);
            if (operation is null)
            {
                return executed;
            }
            var handler = scope.ServiceProvider.GetServices<IOperationHandler>().Single(value => value.Type == operation.Type);
            var outcome = await handler.HandleAsync(operation, CancellationToken.None);
            if (outcome.Completed)
            {
                await repository.CompleteAsync(operation.Id, "integration-test", outcome.Code, CancellationToken.None);
            }
            else
            {
                await repository.FailAsync(operation.Id, "integration-test", outcome.Code, CancellationToken.None);
            }
            executed++;
        }
        throw new InvalidOperationException("Operations did not drain.");
    }

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/import/batches", form);
    }

    public static async Task<ImportBatchResponse> UploadBatchAsync(HttpClient client, string csv, string fileName = "candidatos.csv")
    {
        using var response = await UploadAsync(client, fileName, Encoding.UTF8.GetBytes(csv));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ImportBatchResponse>())!;
    }

    public static async Task<ImportBatchResponse> GetBatchAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ImportBatchResponse>($"/api/import/batches/{id}"))!;

    public static Task<HttpResponseMessage> ValidateAsync(HttpClient client, ImportBatchResponse batch) =>
        client.PostAsJsonAsync($"/api/import/batches/{batch.Id}/validation", new { version = batch.Version });

    public static Task<HttpResponseMessage> CommitAsync(HttpClient client, ImportBatchResponse batch) =>
        client.PostAsJsonAsync($"/api/import/batches/{batch.Id}/commit", new { version = batch.Version });

    /// <summary>Uploads, scans and validates a file, returning the validated batch.</summary>
    public static async Task<ImportBatchResponse> UploadAndValidateAsync(HttpClient client, IServiceProvider services, string csv, string fileName = "candidatos.csv")
    {
        var uploaded = await UploadBatchAsync(client, csv, fileName);
        await RunOperationsAsync(services);
        var scanned = await GetBatchAsync(client, uploaded.Id);
        using var validation = await ValidateAsync(client, scanned);
        validation.EnsureSuccessStatusCode();
        await RunOperationsAsync(services);
        return await GetBatchAsync(client, uploaded.Id);
    }

    public static string Csv(params string[] rows) => Header + "\n" + string.Join("\n", rows) + "\n";
}

/// <summary>A test actor whose permissions the test chooses.</summary>
internal sealed class ImportTestActor(bool authenticated, params string[] permissions) : ICurrentActor
{
    public static ImportTestActor Importer => new(true, Permissions.CandidatesImport, Permissions.CandidatesRead);

    public string? ExternalKey => authenticated ? "import-integration-actor" : null;

    public Guid? UserId => null;

    public bool IsAuthenticated => authenticated;

    public bool HasPermission(string permission) => authenticated && permissions.Contains(permission, StringComparer.Ordinal);
}

/// <summary>Counts every attempt to read an import file, so a test can prove none happened.</summary>
internal sealed class CountingImportRowReader(IImportRowReader inner) : IImportRowReader
{
    private int _reads;

    public int Reads => _reads;

    public string ContentType => inner.ContentType;

    public async IAsyncEnumerable<ImportFileRow> ReadAsync(
        Stream content,
        IReadOnlyCollection<string> requiredColumns,
        IReadOnlyCollection<string> knownColumns,
        ImportLimits limits,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        await foreach (var row in inner.ReadAsync(content, requiredColumns, knownColumns, limits, cancellationToken))
        {
            yield return row;
        }
    }
}

internal sealed class CountingImportFileInspector(IImportFileInspector inner) : IImportFileInspector
{
    private int _inspections;

    public int Inspections => _inspections;

    public Task<ImportFileInspection> InspectAsync(Stream content, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _inspections);
        return inner.InspectAsync(content, cancellationToken);
    }
}
