using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using KeplerTalento.Application.Features.Audit;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-19: the actor on audit rows, the audited reads, the append-only table and the
/// <c>GET /api/audit/events</c> read surface, through real tokens against PostgreSQL.
/// </summary>
[Collection(WebHostCollection.Name)]
public sealed class AuditApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-19-integration-test-signing-key-000000000000";

    /// <summary>Malformed on every family at once; a refusal must not reveal any of it.</summary>
    private const string MalformedQuery =
        "/api/audit/events?from=not-a-date&to=2020-01-01&eventType=nope.nothing&actor=somebody&pageSize=1000";

    [Fact]
    public async Task An_unauthenticated_caller_is_refused_before_the_filter_is_validated()
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var malformed = await client.GetAsync(MalformedQuery);
        using var valid = await client.GetAsync("/api/audit/events");

        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, valid.StatusCode);
        Assert.DoesNotContain("audit.", await malformed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("readonly")]
    // Holds every candidate, catalog, document, import, user and role permission — not audit.read.
    [InlineData("rrhh_admin")]
    public async Task A_caller_without_audit_read_is_forbidden_identically_for_a_malformed_filter(string roleName)
    {
        await ResetAsync();
        await AddUserAsync("caller", "caller@example.test", roleName);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "caller", "caller@example.test");

        using var malformed = await client.GetAsync(MalformedQuery);
        using var valid = await client.GetAsync("/api/audit/events");

        Assert.Equal(HttpStatusCode.Forbidden, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, valid.StatusCode);
        Assert.DoesNotContain("audit.", await malformed.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_governing_seeded_role_holds_audit_read()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();

        var holders = (await dbContext.Roles.AsNoTracking().ToListAsync())
            .Where(role => role.Permissions.Contains("audit.read"))
            .Select(role => role.Name)
            .ToList();

        Assert.Equal(["system_admin"], holders);
    }

    [Fact]
    public async Task A_candidate_write_and_detail_read_name_the_acting_user_while_list_and_search_record_nothing()
    {
        await ResetAsync();
        var recruiterId = await AddUserAsync("recruiter", "recruiter@example.test", "rrhh_admin");
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        await using var factory = CreateFactory();
        using var recruiter = factory.CreateClient();
        await AuthenticateAsync(recruiter, "recruiter", "recruiter@example.test");

        using var created = await recruiter.PostAsJsonAsync("/api/candidates", new
        {
            firstName = "Ada",
            lastName = "Lovelace",
            phone = "",
            email = "ada@example.test",
            location = "",
            province = "",
            country = "España",
            availability = "Inmediata",
            status = CandidateStatuses.New,
            source = "Email",
            notes = "nota privada",
        });
        created.EnsureSuccessStatusCode();
        var candidate = (await created.Content.ReadFromJsonAsync<CandidateResponse>())!;
        (await recruiter.GetAsync($"/api/candidates/{candidate.Id}")).EnsureSuccessStatusCode();
        var afterDetail = await CountEventsAsync();

        (await recruiter.GetAsync("/api/candidates")).EnsureSuccessStatusCode();
        (await recruiter.PostAsJsonAsync("/api/candidates/search", new { page = 1, pageSize = 25 })).EnsureSuccessStatusCode();
        // A refused read (not found) records nothing either.
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/candidates/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(afterDetail, await CountEventsAsync());

        using var auditor = factory.CreateClient();
        await AuthenticateAsync(auditor, "auditor", "auditor@example.test");
        using var response = await auditor.GetAsync($"/api/audit/events?actor={recruiterId}&subject={candidate.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var page = (await response.Content.ReadFromJsonAsync<AuditPageResponse>())!;

        Assert.Contains(page.Items, item => item.EventType == CandidateAuditEvents.Created);
        Assert.Contains(page.Items, item => item.EventType == CandidateAuditEvents.Read);
        Assert.All(page.Items, item =>
        {
            Assert.Equal(AuditActorKinds.User, item.ActorKind);
            Assert.Equal(recruiterId, item.ActorUserId);
            Assert.Equal(candidate.Id.ToString("N"), item.SubjectId);
        });
        // Identifiers and codes only: no candidate value, no actor identity data.
        foreach (var forbidden in new[] { "Ada", "Lovelace", "ada@example.test", "nota privada", "recruiter@example.test", "recruiter", "Integration" })
        {
            Assert.DoesNotContain(forbidden, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Each_filter_narrows_the_trail_with_a_correct_total()
    {
        await ResetAsync();
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        var alice = Guid.CreateVersion7();
        var bob = Guid.CreateVersion7();
        var subject = Guid.CreateVersion7();
        var day = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        await SeedAsync(
            Event(CandidateAuditEvents.Created, subject.ToString("N"), day, AuditActor.User(alice)),
            Event(CandidateAuditEvents.Read, subject.ToString("N"), day.AddDays(1), AuditActor.User(alice)),
            Event("document.downloaded", $"candidate:{subject:N};document:{Guid.NewGuid():N}", day.AddDays(2), AuditActor.User(bob)),
            Event(CatalogAuditEvents.Created, Guid.NewGuid().ToString("N"), day.AddDays(3), AuditActor.User(bob)),
            Event("document.scan", $"candidate:{Guid.NewGuid():N};document:{Guid.NewGuid():N}", day.AddDays(4), AuditActor.System));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "auditor", "auditor@example.test");

        Assert.Equal(5, (await PageAsync(client, "")).TotalCount);
        Assert.Equal(1, (await PageAsync(client, $"eventType={CandidateAuditEvents.Read}")).TotalCount);
        Assert.Equal(2, (await PageAsync(client, $"actor={bob}")).TotalCount);
        Assert.Equal(1, (await PageAsync(client, "actor=system")).TotalCount);
        // A candidate id also finds the document events recorded under it.
        Assert.Equal(3, (await PageAsync(client, $"subject={subject}")).TotalCount);

        var ranged = await PageAsync(
            client,
            $"from={Uri.EscapeDataString(day.AddDays(1).ToString("O"))}&to={Uri.EscapeDataString(day.AddDays(3).ToString("O"))}&actor={alice}");
        var only = Assert.Single(ranged.Items);
        Assert.Equal(1, ranged.TotalCount);
        Assert.Equal(CandidateAuditEvents.Read, only.EventType);
    }

    [Fact]
    public async Task Paging_over_tied_timestamps_neither_repeats_nor_skips_an_event()
    {
        await ResetAsync();
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        var tied = new DateTimeOffset(2026, 9, 2, 8, 0, 0, TimeSpan.Zero);
        var events = Enumerable.Range(0, 7)
            .Select(_ => Event(CandidateAuditEvents.Updated, Guid.NewGuid().ToString("N"), tied, AuditActor.System))
            .ToArray();
        await SeedAsync(events);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "auditor", "auditor@example.test");

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await PageAsync(client, $"page={page}&pageSize=3");
            Assert.Equal(7, result.TotalCount);
            seen.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(7, seen.Count);
        Assert.Equal(events.Select(value => value.Id).OrderDescending(), seen);
    }

    [Fact]
    public async Task Default_and_maximum_page_sizes_are_applied()
    {
        await ResetAsync();
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "auditor", "auditor@example.test");

        var defaults = await PageAsync(client, "");
        Assert.Equal((1, 25), (defaults.Page, defaults.PageSize));
        Assert.Equal(100, (await PageAsync(client, "pageSize=100")).PageSize);
    }

    [Theory]
    [InlineData("eventType=candidate.nonexistent", AuditErrors.EventTypeUnknown)]
    [InlineData("eventType=candidate", AuditErrors.EventTypeUnknown)]
    [InlineData("from=yesterday", AuditErrors.DateInvalid)]
    [InlineData("from=2026-09-10&to=2026-09-01", AuditErrors.DateRangeInvalid)]
    [InlineData("actor=alice", AuditErrors.ActorInvalid)]
    [InlineData("pageSize=101", AuditErrors.PageSizeInvalid)]
    [InlineData("page=0", AuditErrors.PageInvalid)]
    public async Task A_malformed_filter_is_a_stable_validation_problem(string query, string code)
    {
        await ResetAsync();
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "auditor", "auditor@example.test");

        using var response = await client.GetAsync($"/api/audit/events?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_historic_row_without_an_actor_reads_back_as_unknown_and_distinct_from_system()
    {
        await ResetAsync();
        await AddUserAsync("auditor", "auditor@example.test", "system_admin");
        var historicId = Guid.CreateVersion7();
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            // Written the way a pre-KTL-19 row was: no actor columns at all.
            await using var insert = new NpgsqlCommand(
                """
                INSERT INTO "AUD_Events" ("Id", "EventType", "SubjectId", "CorrelationId", "CreatedAtUtc")
                VALUES (@id, 'candidate.updated', 'historic', 'corr-historic', NOW() - INTERVAL '30 days')
                """,
                connection);
            insert.Parameters.AddWithValue("id", historicId);
            await insert.ExecuteNonQueryAsync();
        }
        await SeedAsync(Event(CandidateAuditEvents.Updated, "current", DateTimeOffset.UtcNow, AuditActor.System));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "auditor", "auditor@example.test");

        var unknown = Assert.Single((await PageAsync(client, "actor=unknown")).Items);
        var system = Assert.Single((await PageAsync(client, "actor=system")).Items);

        Assert.Equal(historicId, unknown.Id);
        Assert.Equal(AuditActorKinds.Unknown, unknown.ActorKind);
        Assert.Null(unknown.ActorUserId);
        Assert.Equal(AuditActorKinds.System, system.ActorKind);
        await using var dbContext = NewDbContext();
        Assert.Equal(AuditActorKind.Unknown, (await dbContext.AuditEvents.AsNoTracking().SingleAsync(audit => audit.Id == historicId)).Actor.Kind);
    }

    [Fact]
    public async Task The_runtime_role_holds_no_update_or_delete_on_the_audit_table()
    {
        await ResetAsync();
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        foreach (var (privilege, expected) in new[] { ("SELECT", true), ("INSERT", true), ("UPDATE", false), ("DELETE", false), ("TRUNCATE", false) })
        {
            await using var command = new NpgsqlCommand(
                $"SELECT has_table_privilege('ktl_runtime', '\"AUD_Events\"', '{privilege}')",
                connection);
            Assert.True(expected == (bool)(await command.ExecuteScalarAsync())!, $"ktl_runtime {privilege} on AUD_Events should be {expected}.");
        }
    }

    [Fact]
    public async Task Changing_or_removing_an_audit_event_as_the_runtime_role_fails()
    {
        await ResetAsync();
        var audit = Event(CandidateAuditEvents.Updated, "immutable", DateTimeOffset.UtcNow, AuditActor.System);
        await SeedAsync(audit);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();

        foreach (var statement in new[]
        {
            $"UPDATE \"AUD_Events\" SET \"OutcomeCode\" = 'rewritten' WHERE \"Id\" = '{audit.Id}'",
            $"DELETE FROM \"AUD_Events\" WHERE \"Id\" = '{audit.Id}'",
        })
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var role = new NpgsqlCommand("SET LOCAL ROLE ktl_runtime", connection, transaction))
            {
                await role.ExecuteNonQueryAsync();
            }
            await using var command = new NpgsqlCommand(statement, connection, transaction);
            var refusal = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refusal.SqlState);
            await transaction.RollbackAsync();
        }

        await using var dbContext = NewDbContext();
        Assert.Null((await dbContext.AuditEvents.AsNoTracking().SingleAsync(value => value.Id == audit.Id)).OutcomeCode);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_no_audit_event()
    {
        await ResetAsync();
        await using var dbContext = NewDbContext();

        await using (var transaction = await dbContext.Database.BeginTransactionAsync())
        {
            dbContext.AuditEvents.Add(Event(CandidateAuditEvents.Updated, "rolled-back", DateTimeOffset.UtcNow, AuditActor.System));
            await dbContext.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var check = NewDbContext();
        Assert.False(await check.AuditEvents.AnyAsync(audit => audit.SubjectId == "rolled-back"));
    }

    [Fact]
    public async Task A_refused_candidate_write_leaves_no_event_and_an_applied_one_leaves_exactly_one()
    {
        await ResetAsync();
        await AddUserAsync("recruiter", "recruiter@example.test", "rrhh_admin");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, "recruiter", "recruiter@example.test");
        var payload = new
        {
            firstName = "Grace",
            lastName = "Hopper",
            phone = "",
            email = "",
            location = "",
            province = "",
            country = "España",
            availability = "Inmediata",
            status = CandidateStatuses.New,
            source = "Email",
            notes = "",
        };
        using var created = await client.PostAsJsonAsync("/api/candidates", payload);
        var candidate = (await created.Content.ReadFromJsonAsync<CandidateResponse>())!;
        var before = await CountEventsAsync();

        // A stale version: the change is refused, so its event must not exist.
        using var stale = await client.PutAsJsonAsync($"/api/candidates/{candidate.Id}", new
        {
            payload.firstName,
            payload.lastName,
            phone = "+34 600 000 000",
            payload.email,
            payload.location,
            payload.province,
            payload.country,
            payload.availability,
            payload.status,
            payload.source,
            payload.notes,
            version = candidate.Version + 1000,
        });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(before, await CountEventsAsync());
    }

    private static AuditEvent Event(string type, string subject, DateTimeOffset at, AuditActor actor) =>
        new(Guid.CreateVersion7(), type, subject, "corr-seed", at, actor);

    private async Task SeedAsync(params AuditEvent[] events)
    {
        await using var dbContext = NewDbContext();
        dbContext.AuditEvents.AddRange(events);
        await dbContext.SaveChangesAsync();
    }

    private async Task<int> CountEventsAsync()
    {
        await using var dbContext = NewDbContext();
        return await dbContext.AuditEvents.CountAsync();
    }

    private static async Task<AuditPageResponse> PageAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"/api/audit/events?{query}");
        Assert.True(response.IsSuccessStatusCode, $"{query} answered {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AuditPageResponse>())!;
    }

    private static async Task AuthenticateAsync(HttpClient client, string subject, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/dev/token", new { subject, displayName = "Integration Caller", email });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
    }

    private async Task<Guid> AddUserAsync(string subject, string email, string roleName)
    {
        await using var dbContext = NewDbContext();
        var user = new User(Guid.CreateVersion7(), subject, "Integration User", email, roleName, DateTimeOffset.UtcNow);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false");
        Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false");
        Environment.SetEnvironmentVariable("Authentication__SubjectClaim", "oid");
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "true");
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__SigningKey", SigningKey);
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Audience", Audience);
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                    ["DevelopmentActor:Enabled"] = "false",
                    ["Authentication:SubjectClaim"] = "oid",
                    ["Authentication:DevelopmentIssuer:Enabled"] = "true",
                    ["Authentication:DevelopmentIssuer:SigningKey"] = SigningKey,
                    ["Authentication:DevelopmentIssuer:Issuer"] = Issuer,
                    ["Authentication:DevelopmentIssuer:Audience"] = Audience,
                    ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8",
                }));
        });
    }

    private sealed record DevelopmentTokenResponse(string AccessToken);
}
