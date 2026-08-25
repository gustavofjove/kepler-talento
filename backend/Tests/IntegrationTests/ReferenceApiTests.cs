using System.Net;
using System.Net.Http.Json;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Abstractions.Operations;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Operations;
using KeplerTalento.Web.Features.Candidates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

public sealed class ReferenceApiTests
{
    private static readonly Candidate Candidate = new(
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        "Candidata",
        "Sintética",
        DateTimeOffset.Parse("2026-01-01T00:00:00Z"));

    [Fact]
    public async Task Reference_route_returns_candidate_and_correlation()
    {
        await using var factory = CreateFactory("Testing", true, Candidate);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "reference-test-1");

        var response = await client.GetAsync($"/api/reference/candidates/{Candidate.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("reference-test-1", response.Headers.GetValues("X-Correlation-ID").Single());
        var body = await response.Content.ReadFromJsonAsync<ReferenceCandidateResponse>();
        Assert.Equal(Candidate.Id, body?.Id);
    }

    [Fact]
    public async Task Not_found_problem_is_redacted_and_correlated()
    {
        await using var factory = CreateFactory("Testing", true, null);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/reference/candidates/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("candidate.not_found", body, StringComparison.Ordinal);
        Assert.Contains("correlationId", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Infrastructure", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_does_not_register_reference_route()
    {
        await using var factory = CreateFactory("Production", false, Candidate);
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/reference/candidates/{Candidate.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void Production_rejects_development_actor()
    {
        using var factory = CreateFactory("Production", true, Candidate);
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public async Task Operation_diagnostics_support_redacted_correlation_lookup()
    {
        var operation = new Operation(
            Guid.NewGuid(),
            "document.scan",
            "diagnostic-correlation",
            "document:private-value:scan",
            DateTimeOffset.UtcNow);
        Assert.True(operation.TryClaim("worker", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)));
        Assert.True(operation.Fail("worker", "scanner.unavailable", DateTimeOffset.UtcNow));
        await using var factory = CreateFactory("Testing", true, Candidate, new StubOperations(operation));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reference/operations?correlationId=diagnostic-correlation");
        var body = await response.Content.ReadAsStringAsync();
        var operations = await response.Content.ReadFromJsonAsync<ReferenceCandidateEndpoints.ReferenceOperationResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.Single(operations!);
        Assert.Equal(operation.Id, result.Id);
        Assert.Equal("scanner.unavailable", result.OutcomeCode);
        Assert.Equal("diagnostic-correlation", result.CorrelationId);
        Assert.DoesNotContain("private-value", body, StringComparison.Ordinal);
        Assert.DoesNotContain("idempotency", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage", body, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string environment,
        bool actorEnabled,
        Candidate? candidate,
        IOperationRepository? operations = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevelopmentActor:Enabled"] = actorEnabled.ToString(),
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICandidateReader>();
                services.AddScoped<ICandidateReader>(_ => new StubReader(candidate));
                if (operations is not null)
                {
                    services.RemoveAll<IOperationRepository>();
                    services.AddScoped(_ => operations);
                }
            });
        });

    private sealed class StubReader(Candidate? candidate) : ICandidateReader
    {
        public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(candidate);
    }

    private sealed class StubOperations(Operation operation) : IOperationRepository
    {
        public Task<Operation> EnqueueAsync(string type, string correlationId, string idempotencyKey, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Operation?> ClaimNextAsync(string owner, TimeSpan lease, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> RenewLeaseAsync(Guid id, string owner, TimeSpan lease, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> CompleteAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> FailAsync(Guid id, string owner, string outcomeCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> CancelAsync(Guid id, string? owner, string outcomeCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Operation?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Operation?>(id == operation.Id ? operation : null);
        public Task<IReadOnlyList<Operation>> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Operation>>(
                correlationId == operation.CorrelationId ? [operation] : []);
    }
}
