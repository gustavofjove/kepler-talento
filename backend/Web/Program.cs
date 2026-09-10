using FastEndpoints;
using FastEndpoints.Swagger;
using KeplerTalento.Application;
using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Infrastructure;
using KeplerTalento.Web.Correlation;
using KeplerTalento.Web.Errors;
using KeplerTalento.Web.Features.Candidates;
using KeplerTalento.Web.Features.Catalogs;
using KeplerTalento.Web.Health;
using KeplerTalento.Web.Identity;
using KeplerTalento.Web.Observability;
using KeplerTalento.Infrastructure.Persistence;
using KeplerTalento.Infrastructure.Documents;
using Microsoft.AspNetCore.HttpOverrides;
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
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = DocumentStorageOptions.AbsoluteMaximumBytes);
builder.Services.AddFastEndpoints();
builder.Services.SwaggerDocument(settings =>
{
    settings.EnableJWTBearerAuth = false;
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
builder.Services.AddScoped<DevelopmentActor>();
builder.Services.AddScoped<ICurrentActor>(provider => provider.GetRequiredService<DevelopmentActor>());
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
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseInitializer.MigrateAsync(dbContext, app.Lifetime.ApplicationStopping);
    await DatabaseInitializer.SeedCatalogsAsync(dbContext, app.Lifetime.ApplicationStopping);
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
app.UseFastEndpoints();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "healthy" })).WithTags("Health");
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
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);
app.MapCatalogEndpoints();
app.MapCandidateEndpoints();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwaggerGen();
}
app.Run();

public partial class Program;
