using FastEndpoints;
using FastEndpoints.Swagger;
using KeplerTalento.Application;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Infrastructure;
using KeplerTalento.Web.Correlation;
using KeplerTalento.Web.Errors;
using KeplerTalento.Web.Features.Admin;
using KeplerTalento.Web.Features.Candidates;
using KeplerTalento.Web.Features.Catalogs;
using KeplerTalento.Web.Features.Documents;
using KeplerTalento.Web.Features.Import;
using KeplerTalento.Web.Features.Search;
using KeplerTalento.Web.Health;
using KeplerTalento.Web.Identity;
using KeplerTalento.Web.Observability;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Documents;
using KeplerTalento.Web.Features.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using NJsonSchema;
using NSwag;
using NSwag.Generation.Processors;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    // Registered last so it sees every property the other enrichers added, and applies to
    // every sink. Candidate personal data must not reach a log whichever slice, library or
    // unhandled exception put it in the event.
    .Enrich.With<PersonalDataRedactionEnricher>()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = DocumentStorageOptions.AbsoluteMaximumBytes + DocumentStorageOptions.MultipartEnvelopeBytes);
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MultipartBodyLengthLimit = DocumentStorageOptions.AbsoluteMaximumBytes + DocumentStorageOptions.MultipartEnvelopeBytes);
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(settings =>
{
    // The API takes a bearer token since KTL-16, so the document says so and Swagger UI can
    // carry one.
    settings.EnableJWTBearerAuth = true;
    settings.DocumentSettings = document =>
    {
        document.Title = "Kepler Talento API";
        document.Version = "v1";
        document.OperationProcessors.Add(new OperationProcessor(context =>
        {
            context.OperationDescription.Operation.Parameters.Add(new OpenApiParameter
            {
                Name = CorrelationMiddleware.HeaderName,
                Kind = OpenApiParameterKind.Header,
                Type = JsonObjectType.String,
                IsRequired = false,
                Description = "Optional caller correlation identifier; a safe identifier is generated when absent or invalid.",
            });
            return true;
        }));
    };
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"])
    .AddCheck<StorageReadinessHealthCheck>("storage", tags: ["ready"])
    .AddCheck<ScannerHealthCheck>("scanner", tags: ["scanner"]);
builder.Services.AddOptions<DevelopmentActorOptions>()
    .Bind(builder.Configuration.GetSection(DevelopmentActorOptions.SectionName))
    .Validate(
        options => !builder.Environment.IsProduction() || !options.Enabled,
        "DevelopmentActor cannot be enabled in Production.")
    .ValidateOnStart();

builder.Services.AddOptions<KeplerAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(KeplerAuthenticationOptions.SectionName))
    .Validate(
        // The dev issuer is a way to mint a token for any subject you name. In Production that
        // is not a convenience, it is an unauthenticated path to any identity.
        options => !builder.Environment.IsProduction() || options.DevelopmentIssuer?.Enabled != true,
        "Authentication:DevelopmentIssuer cannot be enabled in Production.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.SubjectClaim),
        "Authentication:SubjectClaim is required.")
    .ValidateOnStart();

var authenticationOptions = builder.Configuration.GetSection(KeplerAuthenticationOptions.SectionName)
    .Get<KeplerAuthenticationOptions>() ?? new();
var developmentIssuer = authenticationOptions.DevelopmentIssuer;
var developmentIssuerEnabled = developmentIssuer?.Enabled == true && !builder.Environment.IsProduction();
if (builder.Environment.IsProduction() && developmentIssuer?.Enabled == true)
{
    throw new InvalidOperationException("Authentication:DevelopmentIssuer cannot be enabled in Production.");
}

var developmentActorOptions = builder.Configuration.GetSection(DevelopmentActorOptions.SectionName)
    .Get<DevelopmentActorOptions>() ?? new();

// A branch, never a chain. Whichever of the two is registered is the only ICurrentActor there
// is, which is what makes "an absent or invalid token never resolves to the development actor"
// a property of the composition rather than an assertion about a fallback (design D11).
if (developmentActorOptions.Enabled)
{
    builder.Services.AddScoped<DevelopmentActor>();
    builder.Services.AddScoped<ICurrentActor>(provider => provider.GetRequiredService<DevelopmentActor>());
}
else
{
    builder.Services.AddScoped<TokenCurrentActor>();
    builder.Services.AddScoped<ICurrentActor>(provider => provider.GetRequiredService<TokenCurrentActor>());
    builder.Services.AddScoped<CallerIdentityResolver>();
}

if (developmentIssuerEnabled)
{
    builder.Services.AddSingleton<DevelopmentTokenIssuer>();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Signing keys come from the discovery document and refresh on their own, which is what
        // survives a key rotation at the provider without restarting the API (design D1).
        options.Authority = authenticationOptions.Authority;
        options.Audience = authenticationOptions.Audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // 60 seconds rather than the five-minute default: a token just outside its window
            // is refused, and the scenario the specs name is clock skew, not a grace period.
            ClockSkew = TimeSpan.FromSeconds(60),
        };

        if (developmentIssuerEnabled)
        {
            // The dev issuer is trusted *in addition to* the tenant, and its tokens go through
            // this same pipeline. Nothing here weakens validation; it adds one more issuer and
            // one more key that a signature must match.
            var key = DevelopmentTokenIssuer.SigningKey(developmentIssuer!);
            var issuers = new List<string> { developmentIssuer!.Issuer };
            var audiences = new List<string> { developmentIssuer.Audience };
            if (!string.IsNullOrWhiteSpace(authenticationOptions.Authority))
            {
                issuers.Add(authenticationOptions.Authority!);
            }
            if (!string.IsNullOrWhiteSpace(authenticationOptions.Audience))
            {
                audiences.Add(authenticationOptions.Audience!);
            }
            options.TokenValidationParameters.ValidIssuers = issuers;
            options.TokenValidationParameters.ValidAudiences = audiences;
            options.TokenValidationParameters.IssuerSigningKeys = [key];
            // Without an authority there is no discovery document to fetch, and asking for one
            // would make start-up depend on a tenant the developer does not have.
            options.RequireHttpsMetadata = false;
            if (string.IsNullOrWhiteSpace(authenticationOptions.Authority))
            {
                options.Authority = null;
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
            }
        }
    });
builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires an authenticated caller unless it opts out with AllowAnonymous,
    // which only health and the development token issuer do. A fallback policy rather than
    // .RequireAuthorization() per group because the default must be "refused": a business
    // endpoint group added later without an authorization line then fails closed instead of
    // being silently open.
    //
    // It is not applied when the development actor is registered, because there is no token to
    // authenticate in that mode and the handler guards are what decide. That mode cannot exist
    // in Production - start-up fails - so no deployment reaches this with the fallback off.
    if (!developmentActorOptions.Enabled)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }

    // Endpoint policies run before minimal-API body binding. The handlers repeat these guards,
    // but a handler-only check is too late for malformed JSON: model binding would disclose a
    // 400 to a caller who should learn only that administration is forbidden (design D6).
    options.AddPolicy(Permissions.UsersManage, policy => policy.RequireAssertion(context =>
        context.Resource is HttpContext httpContext
        && httpContext.RequestServices.GetRequiredService<ICurrentActor>() is { } actor
        && actor.IsAuthenticated
        && actor.HasPermission(Permissions.UsersManage)));
    options.AddPolicy(Permissions.RolesManage, policy => policy.RequireAssertion(context =>
        context.Resource is HttpContext httpContext
        && httpContext.RequestServices.GetRequiredService<ICurrentActor>() is { } actor
        && actor.IsAuthenticated
        && actor.HasPermission(Permissions.RolesManage)));
    // KTL-17. Before the multipart form is read, so an unauthorized upload's bytes are never
    // parsed or stored, and before body binding, so a malformed request is refused identically.
    options.AddPolicy(Permissions.CandidatesImport, policy => policy.RequireAssertion(context =>
        context.Resource is HttpContext httpContext
        && httpContext.RequestServices.GetRequiredService<ICurrentActor>() is { } actor
        && actor.IsAuthenticated
        && actor.HasPermission(Permissions.CandidatesImport)));
});
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddScoped<ICorrelationContext>(provider => provider.GetRequiredService<CorrelationContext>());
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    var trustedNetworks = builder.Configuration.GetSection("ProxyTrust:KnownNetworks").Get<string[]>() ?? [];
    if (trustedNetworks.Length == 0)
    {
        throw new InvalidOperationException("ProxyTrust:KnownNetworks must contain at least one explicit CIDR.");
    }
    foreach (var trustedNetwork in trustedNetworks)
    {
        if (!System.Net.IPNetwork.TryParse(trustedNetwork, out var network))
        {
            throw new InvalidOperationException("ProxyTrust:KnownNetworks contains an invalid CIDR.");
        }
        options.KnownIPNetworks.Add(network);
    }
});

var actorOptions = builder.Configuration.GetSection(DevelopmentActorOptions.SectionName).Get<DevelopmentActorOptions>() ?? new();
if (builder.Environment.IsProduction() && actorOptions.Enabled)
{
    throw new InvalidOperationException("DevelopmentActor cannot be enabled in Production.");
}

var app = builder.Build();
if (args.Contains("--reconcile", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var reconciler = scope.ServiceProvider.GetRequiredService<DocumentStorageReconciler>();
    var report = await reconciler.ReconcileAsync(TimeSpan.FromHours(24), app.Lifetime.ApplicationStopping);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
    if (!report.IsSuccessful) Environment.ExitCode = 2;
    return;
}
if (args.Contains("--purge-imports", StringComparer.Ordinal))
{
    // The same purge the worker schedules, runnable on demand the way --reconcile is.
    await using var scope = app.Services.CreateAsyncScope();
    var purge = scope.ServiceProvider.GetRequiredService<KeplerTalento.Infrastructure.Import.ImportPurgeHandler>();
    var report = await purge.PurgeAsync(DateTimeOffset.UtcNow, app.Lifetime.ApplicationStopping);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
    return;
}
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseInitializer.MigrateAsync(dbContext, app.Lifetime.ApplicationStopping);
    await DatabaseInitializer.SeedCatalogsAsync(dbContext, app.Lifetime.ApplicationStopping);
    // Runs last and throws when the installation would be left with nobody able to administer
    // it. Deploying on into a locked-out installation is the failure this is here to prevent,
    // so it fails the migrator rather than logging a warning nobody reads.
    await DatabaseInitializer.SeedBootstrapAdministratorAsync(
        dbContext,
        authenticationOptions.BootstrapAdministrator?.Email,
        authenticationOptions.BootstrapAdministrator?.DisplayName,
        app.Lifetime.ApplicationStopping);
    return;
}
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    await next();
});
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});
// After the correlation middleware, so a refused request still carries a correlation id, and
// before endpoint dispatch (design D1).
app.UseAuthentication();
// DevelopmentActor is a local convenience for requests carrying no credentials, never a
// fallback for a bad credential. If a caller presents a bearer token, authentication must have
// accepted it; otherwise refuse before any handler can see the synthetic actor.
if (developmentActorOptions.Enabled)
{
    app.Use(async (context, next) =>
    {
        var carriesBearer = context.Request.Headers.Authorization
            .Any(value => value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true);
        if (carriesBearer && context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next();
    });
}
// Turns the validated token into the ICurrentActor the handler guards consult. Between
// authentication and authorization, because it is the step that decides what the caller may do.
// Part of the token branch: in development-actor mode there is no token to resolve, and the
// resolver it depends on is not registered (design D11).
if (!developmentActorOptions.Enabled)
{
    app.UseMiddleware<IdentityResolutionMiddleware>();
}
app.UseAuthorization();
app.UseFastEndpoints();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "healthy" })).WithTags("Health")
    .AllowAnonymous();
app.MapGet("/api/health/ready", async (
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthChecks,
        CancellationToken cancellationToken) =>
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            cancellationToken);
        return report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "unhealthy" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .WithTags("Health")
    .AllowAnonymous()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);
app.MapGet("/api/health/scanner", async (
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService healthChecks,
        CancellationToken cancellationToken) =>
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("scanner"),
            cancellationToken);
        return report.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy
            ? Results.Ok(new { status = "healthy" })
            : Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    })
    .WithTags("Health")
    .AllowAnonymous()
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);
app.MapCatalogEndpoints();
app.MapCandidateEndpoints();
app.MapDocumentEndpoints();
app.MapImportEndpoints();
app.MapSearchEndpoints();
app.MapAdminEndpoints();
app.MapMeEndpoints();
if (developmentIssuerEnabled)
{
    // Anonymous by necessity - it is how a caller acquires a token - and therefore mapped only
    // outside Production, where start-up would already have failed.
    app.MapDevelopmentTokenEndpoints();
}
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwaggerGen();
}
app.Run();

public partial class Program;
