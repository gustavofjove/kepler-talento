using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using KeplerTalento.Application.Features.Admin;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

[Collection(WebHostCollection.Name)]
public sealed class AdminApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-16-integration-test-signing-key-000000000000";
    private const string OtherSigningKey = "ktl-16-invalid-test-signing-key-0000000000000";

    public static TheoryData<string, string> AdminEndpoints => new()
    {
        { "GET", "/api/admin/users" },
        { "GET", $"/api/admin/users/{Guid.Empty}" },
        { "POST", "/api/admin/users" },
        { "PUT", $"/api/admin/users/{Guid.Empty}" },
        { "PUT", $"/api/admin/users/{Guid.Empty}/role" },
        { "PUT", $"/api/admin/users/{Guid.Empty}/active" },
        { "GET", "/api/admin/roles" },
        { "GET", $"/api/admin/roles/{Guid.Empty}" },
        { "POST", "/api/admin/roles" },
        { "PUT", $"/api/admin/roles/{Guid.Empty}" },
        { "PUT", $"/api/admin/roles/{Guid.Empty}/permissions" },
        { "PUT", $"/api/admin/roles/{Guid.Empty}/active" },
    };

    [Fact]
    public void Development_actor_cannot_start_in_production()
    {
        var previousActor = Environment.GetEnvironmentVariable("DevelopmentActor__Enabled");
        var previousIssuer = Environment.GetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
            Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
            Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "false");

            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseEnvironment("Production"));

            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
            Assert.Contains("DevelopmentActor cannot be enabled in Production", exception.ToString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", previousActor);
            Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", previousIssuer);
        }
    }

    [Fact]
    public async Task Revoking_a_role_permission_changes_the_same_tokens_next_request()
    {
        await ResetAsync();
        await AddUserAsync("admin-subject", "admin@example.test", active: true, roleName: "rrhh_admin");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "admin-subject", "admin@example.test");

        await AssertStatusAsync(client, "GET", "/api/admin/users", token, HttpStatusCode.OK);

        await using (var dbContext = NewDbContext())
        {
            var role = await dbContext.Roles.SingleAsync(value => value.Name == "rrhh_admin");
            role.ReplacePermissions(
                role.Permissions.Where(permission => permission != "users.manage"),
                DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        await AssertStatusAsync(client, "GET", "/api/admin/users", token, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Invalid_bearer_is_refused_instead_of_falling_back_to_the_development_actor()
    {
        await ResetAsync();
        await using var factory = CreateDevelopmentActorFactory();
        using var client = factory.CreateClient();

        await AssertStatusAsync(
            client,
            "GET",
            "/api/admin/users",
            "not-a-valid-token",
            HttpStatusCode.Unauthorized);

        await AssertStatusAsync(client, "GET", "/api/admin/users", token: null, HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_listing_is_paged_and_excludes_inactive_users_by_default()
    {
        await ResetAsync();
        await AddUserAsync("admin-subject", "admin@example.test", active: true, roleName: "rrhh_admin");
        await AddUserAsync("active-subject", "active@example.test", active: true);
        await AddUserAsync("inactive-subject", "inactive@example.test", active: false);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "admin-subject", "admin@example.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var defaultPage = await client.GetFromJsonAsync<UserPageResponse>(
            "/api/admin/users?page=1&pageSize=1");
        Assert.NotNull(defaultPage);
        Assert.Single(defaultPage.Items);
        Assert.Equal(2, defaultPage.TotalCount);
        Assert.All(defaultPage.Items, user => Assert.True(user.IsActive));

        var allUsers = await client.GetFromJsonAsync<UserPageResponse>(
            "/api/admin/users?includeInactive=true&page=1&pageSize=10");
        Assert.NotNull(allUsers);
        Assert.Equal(3, allUsers.TotalCount);
        Assert.Contains(allUsers.Items, user => !user.IsActive);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Every_admin_endpoint_refuses_missing_and_invalid_identity_before_validation(
        string method,
        string path)
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        await AssertStatusAsync(client, method, path, token: null, HttpStatusCode.Unauthorized);
        await AssertStatusAsync(
            client,
            method,
            path,
            IssueToken("readonly-expired", expires: DateTime.UtcNow.AddMinutes(-2)),
            HttpStatusCode.Unauthorized);
        await AssertStatusAsync(
            client,
            method,
            path,
            IssueToken("readonly-invalid", signingKey: OtherSigningKey),
            HttpStatusCode.Unauthorized);
        await AssertStatusAsync(
            client,
            method,
            path,
            IssueToken("readonly-audience", audience: "wrong-audience"),
            HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Every_admin_endpoint_refuses_a_caller_without_permission_before_validation(
        string method,
        string path)
    {
        await ResetAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "readonly-subject", "readonly@example.test");

        await AssertStatusAsync(
            client,
            method,
            path,
            token,
            HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Every_admin_endpoint_refuses_a_deactivated_user_with_a_valid_token_before_validation(
        string method,
        string path)
    {
        await ResetAsync();
        await AddUserAsync("inactive-subject", "inactive@example.test", active: false);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "inactive-subject", "inactive@example.test");

        await AssertStatusAsync(
            client,
            method,
            path,
            token,
            HttpStatusCode.Unauthorized);
    }

    private static async Task AssertStatusAsync(
        HttpClient client,
        string method,
        string path,
        string? token,
        HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT")
        {
            // Deliberately malformed for every write. Authentication/authorization must win.
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<string> MintTokenAsync(HttpClient client, string subject, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/dev/token", new
        {
            subject,
            displayName = "Integration Caller",
            email,
        });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>();
        return result!.AccessToken;
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
    }

    private async Task AddUserAsync(
        string subject,
        string email,
        bool active,
        string roleName = "readonly")
    {
        await using var dbContext = NewDbContext();
        var user = new User(
            Guid.CreateVersion7(),
            subject,
            "Integration User",
            email,
            roleName,
            DateTimeOffset.UtcNow);
        if (!active)
        {
            user.Deactivate(DateTimeOffset.UtcNow);
        }
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).Options);

    private WebApplicationFactory<Program> CreateFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "false");
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

    private WebApplicationFactory<Program> CreateDevelopmentActorFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
        Environment.SetEnvironmentVariable("Authentication__DevelopmentIssuer__Enabled", "false");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                    ["DevelopmentActor:Enabled"] = "true",
                    ["DevelopmentActor:Permissions:0"] = "users.manage",
                    ["Authentication:DevelopmentIssuer:Enabled"] = "false",
                    ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8",
                }));
        });
    }

    private static string IssueToken(
        string subject,
        string email = "caller@example.test",
        DateTime? expires = null,
        string audience = Audience,
        string signingKey = SigningKey)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: audience,
            claims:
            [
                new Claim("oid", subject),
                new Claim("name", "Integration Caller"),
                new Claim("email", email),
            ],
            notBefore: expires is not null && expires < now ? expires.Value.AddMinutes(-5) : now.AddSeconds(-1),
            expires: expires ?? now.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed record DevelopmentTokenResponse(string AccessToken);
}
