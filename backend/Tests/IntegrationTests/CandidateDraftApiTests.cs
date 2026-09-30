using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Features.Candidates.CvDraft;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Identity;
using KeplerTalento.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace KeplerTalento.Tests.IntegrationTests;

/// <summary>
/// KTL-32: <c>POST /api/candidates/draft-from-document</c> through HTTP, the real readers and
/// extractor, and a real PostgreSQL. The people in the fixtures are fictitious.
/// </summary>
[Collection(WebHostCollection.Name)]
public sealed class CandidateDraftApiTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>, IDisposable
{
    private const string Path = "/api/candidates/draft-from-document";
    private const string Issuer = "https://kepler-talento.test/issuer";
    private const string Audience = "kepler-talento-test-api";
    private const string SigningKey = "ktl-32-integration-test-signing-key-0000000000";
    private const string Marker = "SYNTHETIC-MALWARE-MARKER";

    private const string SentinelFirstName = "Zoraida";
    private const string SentinelLastName = "Villalobos Etxeberria";
    private const string SentinelEmail = "zoraida.villalobos@example.test";
    private const string SentinelFileName = "cv-zoraida-villalobos.pdf";

    private readonly string _storageRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ktl-32-draft-{Guid.NewGuid():N}");

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DocumentStorage__Root", null);
        Environment.SetEnvironmentVariable("CvDraft__MaxConcurrent", null);
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    // ---------------------------------------------------------------- authorization (4.1)

    [Fact]
    public async Task An_unauthenticated_caller_is_refused_and_nothing_is_scanned_or_parsed()
    {
        await ResetAsync();
        var scanner = new CountingScanner();
        var reader = new CountingReader();
        await using var factory = CreateTokenFactory(scanner, reader);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, CvPdf());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, scanner.Scans);
        Assert.Equal(0, reader.Reads);
        await AssertNothingLeftBehindAsync(expectedAudits: 0);
    }

    [Fact]
    public async Task An_unauthenticated_caller_with_a_malformed_body_is_refused_as_unauthenticated()
    {
        await ResetAsync();
        await using var factory = CreateTokenFactory(new CountingScanner(), null);
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(Path, new StringContent("{ not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_caller_with_no_stored_user_is_refused_before_the_body_is_read()
    {
        await ResetAsync();
        var scanner = new CountingScanner();
        var reader = new CountingReader();
        await using var factory = CreateTokenFactory(scanner, reader);
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "unknown-subject", "unknown@example.test");

        using var response = await PostAsync(client, SentinelFileName, CvPdf(), token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, scanner.Scans);
        Assert.Equal(0, reader.Reads);
    }

    [Theory]
    [InlineData(Permissions.CandidatesRead)]
    [InlineData(Permissions.DocumentsUpload)]
    [InlineData(Permissions.CandidatesUpdate)]
    public async Task An_actor_without_candidates_create_is_refused_and_nothing_is_scanned_or_parsed(string permission)
    {
        await ResetAsync();
        var scanner = new CountingScanner();
        var reader = new CountingReader();
        await using var factory = CreateActorFactory(new DraftTestActor(true, permission), scanner, reader);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, CvPdf());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(SentinelFirstName, body, StringComparison.Ordinal);
        Assert.Equal(0, scanner.Scans);
        Assert.Equal(0, reader.Reads);
        await AssertNothingLeftBehindAsync(expectedAudits: 0);
    }

    // ---------------------------------------------------------------- contract (4.2)

    [Fact]
    public async Task A_creator_with_a_real_token_gets_the_suggested_fields()
    {
        await ResetAsync();
        await AddUserAsync("creator-subject", "creator@example.test", "rrhh_user");
        await using var factory = CreateTokenFactory(new CountingScanner(), null);
        using var client = factory.CreateClient();
        var token = await MintTokenAsync(client, "creator-subject", "creator@example.test");

        using var response = await PostAsync(client, SentinelFileName, CvPdf(), token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var draft = (await response.Content.ReadFromJsonAsync<CandidateDraftResponse>())!;
        Assert.Equal(CvDraftCodes.Extracted, draft.Outcome);
        Assert.NotEqual(Guid.Empty, draft.DraftId);
        Assert.Equal(SentinelFirstName, draft.Fields["firstName"].Value);
        Assert.Equal(SentinelLastName, draft.Fields["lastName"].Value);
        Assert.Equal("high", draft.Fields["firstName"].Confidence);
        Assert.Equal(SentinelEmail, draft.Fields["email"].Value);
        Assert.Equal("611 98 76 54", draft.Fields["phone"].Value);
        Assert.Equal("Bilbao", draft.Fields["location"].Value);
        Assert.Equal("Bizkaia", draft.Fields["province"].Value);
    }

    [Fact]
    public async Task The_response_carries_only_the_draft_contract()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, CvPdf());
        var body = await response.Content.ReadAsStringAsync();

        using var json = System.Text.Json.JsonDocument.Parse(body);
        Assert.Equal(["draftId", "outcome", "fields"], json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.All(
            json.RootElement.GetProperty("fields").EnumerateObject(),
            field => Assert.Contains(field.Name, new[] { "firstName", "lastName", "email", "phone", "location", "province" }));
        Assert.DoesNotContain(SentinelFileName, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Experiencia", body, StringComparison.Ordinal);
        Assert.DoesNotContain(_storageRoot, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_docx_is_read_as_well()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "cv.docx", CvDocx());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = (await response.Content.ReadFromJsonAsync<CandidateDraftResponse>())!;
        Assert.Equal(SentinelFirstName, draft.Fields["firstName"].Value);
        Assert.Equal(SentinelEmail, draft.Fields["email"].Value);
    }

    [Fact]
    public async Task An_image_only_pdf_returns_an_empty_draft()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "escaneado.pdf", Pdf(page => page.DrawRectangle(new PdfPoint(50, 50), 200, 300)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var draft = (await response.Content.ReadFromJsonAsync<CandidateDraftResponse>())!;
        Assert.Equal(CvDraftCodes.NoText, draft.Outcome);
        Assert.Empty(draft.Fields);
    }

    [Fact]
    public async Task Infected_content_is_refused_and_never_parsed()
    {
        await ResetAsync();
        var reader = new CountingReader();
        await using var factory = CreateActorFactory(DraftTestActor.Creator, new CountingScanner(), reader);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, [.. CvPdf(), .. Encoding.ASCII.GetBytes($"\n%{Marker}\n")]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(CvDraftCodes.Rejected, await CodeAsync(response));
        Assert.DoesNotContain("synthetic-signature", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, reader.Reads);
        await AssertNothingLeftBehindAsync(expectedAudits: 1);
    }

    [Fact]
    public async Task An_unavailable_scanner_fails_closed_with_a_retryable_code()
    {
        await ResetAsync();
        var reader = new CountingReader();
        await using var factory = CreateActorFactory(
            DraftTestActor.Creator,
            new CountingScanner(new ScanResult(ScanVerdict.Error, "scanner.unavailable")),
            reader);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, CvPdf());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
        Assert.Equal(CvDraftCodes.ScannerUnavailable, await CodeAsync(response));
        Assert.Equal(0, reader.Reads);
    }

    [Theory]
    [InlineData("cv.txt", "texto plano", CvDraftCodes.FormatUnsupported)]
    [InlineData("cv.doc", "no es un doc", CvDraftCodes.FormatUnsupported)]
    [InlineData("cv.pdf", "no es un pdf", CvDraftCodes.ContentRejected)]
    [InlineData("cv.docx", "no es un zip", CvDraftCodes.ContentRejected)]
    public async Task Unsupported_and_mismatched_files_are_refused_before_scanning(string fileName, string content, string code)
    {
        await ResetAsync();
        var scanner = new CountingScanner();
        await using var factory = CreateActorFactory(DraftTestActor.Creator, scanner);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, fileName, Encoding.UTF8.GetBytes(content));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(code, await FieldCodeAsync(response));
        Assert.Equal(0, scanner.Scans);
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, "cv.pdf", []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CvDraftCodes.FileEmpty, await FieldCodeAsync(response));
    }

    [Fact]
    public async Task A_request_without_a_file_part_is_refused()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var form = new MultipartFormDataContent { { new StringContent("x"), "other" } };
        using var multipart = await client.PostAsync(Path, form);
        using var json = await client.PostAsync(Path, new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, multipart.StatusCode);
        Assert.Equal(CvDraftCodes.FileMissing, await FieldCodeAsync(multipart));
        Assert.Equal(HttpStatusCode.BadRequest, json.StatusCode);
        Assert.Equal(CvDraftCodes.FileMissing, await FieldCodeAsync(json));
    }

    [Fact]
    public async Task A_file_over_the_limit_is_refused_before_scanning()
    {
        await ResetAsync();
        var scanner = new CountingScanner();
        await using var factory = CreateActorFactory(DraftTestActor.Creator, scanner);
        using var client = factory.CreateClient();
        var oversized = new byte[(20 * 1024 * 1024) + 1];
        "%PDF-1.7"u8.CopyTo(oversized);

        using var response = await PostAsync(client, "cv.pdf", oversized);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(CvDraftCodes.SizeExceeded, await FieldCodeAsync(response));
        Assert.Equal(0, scanner.Scans);
    }

    // ---------------------------------------------------------------- nothing left behind (4.3)

    [Fact]
    public async Task An_extraction_stores_nothing_and_audits_one_row_without_personal_data()
    {
        await ResetAsync();
        await using var factory = CreateActorFactory(DraftTestActor.Creator);
        using var client = factory.CreateClient();

        using var response = await PostAsync(client, SentinelFileName, CvPdf());
        var draft = (await response.Content.ReadFromJsonAsync<CandidateDraftResponse>())!;

        await AssertNothingLeftBehindAsync(expectedAudits: 1);
        await using var db = NewDbContext();
        var audit = await db.AuditEvents.AsNoTracking().SingleAsync(row => row.EventType == CandidateAuditEvents.CvDraftExtracted);
        Assert.Equal(draft.DraftId.ToString("N"), audit.SubjectId);
        Assert.Equal(CvDraftCodes.Extracted, audit.OutcomeCode);
        Assert.Equal(DraftTestActor.CreatorUserId, audit.ActorUserId);
        var recorded = $"{audit.SubjectId}|{audit.OutcomeCode}|{audit.CorrelationId}";
        Assert.DoesNotContain(SentinelFirstName, recorded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("villalobos", recorded, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_log_line_carries_the_filename_the_text_or_a_suggestion()
    {
        await ResetAsync();
        var capture = new LogCapture();
        await using var factory = CreateActorFactory(DraftTestActor.Creator, loggerCapture: capture);
        using var client = factory.CreateClient();

        using var extracted = await PostAsync(client, SentinelFileName, CvPdf());
        using var infected = await PostAsync(client, SentinelFileName, [.. CvPdf(), .. Encoding.ASCII.GetBytes($"\n%{Marker}\n")]);
        using var unreadable = await PostAsync(client, SentinelFileName, Encoding.ASCII.GetBytes("%PDF-1.7 Zoraida Villalobos roto"));

        Assert.Equal(HttpStatusCode.OK, extracted.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, infected.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unreadable.StatusCode);
        var rendered = capture.Rendered();
        Assert.Contains(CvDraftCodes.Extracted, rendered, StringComparison.Ordinal);
        Assert.Contains(CvDraftCodes.Rejected, rendered, StringComparison.Ordinal);
        Assert.Contains(CvDraftCodes.Unreadable, rendered, StringComparison.Ordinal);
        foreach (var sentinel in new[] { SentinelFirstName, "Villalobos", SentinelEmail, "611", "Bilbao", SentinelFileName })
        {
            Assert.DoesNotContain(sentinel, rendered, StringComparison.OrdinalIgnoreCase);
        }
        await AssertNothingLeftBehindAsync(expectedAudits: 3);
    }

    // ---------------------------------------------------------------- bounded work (4.4)

    [Fact]
    public async Task Extractions_beyond_the_concurrency_bound_are_refused_at_once()
    {
        await ResetAsync();
        var reader = new CountingReader(blockUntilReleased: true);
        await using var factory = CreateActorFactory(DraftTestActor.Creator, reader: reader, maxConcurrent: 1);
        using var client = factory.CreateClient();

        var first = PostAsync(client, SentinelFileName, CvPdf());
        await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        using var second = await PostAsync(client, SentinelFileName, CvPdf());
        reader.Release();
        using var firstResponse = await first;

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(CvDraftCodes.Busy, await CodeAsync(second));
        Assert.NotNull(second.Headers.RetryAfter);
        Assert.Equal(1, reader.Reads);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
    }

    // ---------------------------------------------------------------- fixtures

    private static byte[] CvPdf() => Pdf(page =>
    {
        var font = page.Font;
        page.Builder.AddText($"{SentinelFirstName} {SentinelLastName}", 24, new PdfPoint(50, 780), font);
        page.Builder.AddText("Desarrolladora backend", 12, new PdfPoint(50, 755), font);
        page.Builder.AddText(SentinelEmail, 10, new PdfPoint(50, 735), font);
        page.Builder.AddText("+34 611 987 654", 10, new PdfPoint(50, 720), font);
        page.Builder.AddText("Calle Mayor 3, 48001 Bilbao", 10, new PdfPoint(50, 705), font);
        page.Builder.AddText("Experiencia", 14, new PdfPoint(50, 660), font);
    });

    private sealed record PdfPage(PdfPageBuilder Builder, PdfDocumentBuilder.AddedFont Font)
    {
        public void DrawRectangle(PdfPoint position, double width, double height) => Builder.DrawRectangle(position, width, height);
    }

    private static byte[] Pdf(Action<PdfPage> draw)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        draw(new PdfPage(builder.AddPage(PageSize.A4), font));
        return builder.Build();
    }

    private static byte[] CvDocx()
    {
        const string ns = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
        static string Paragraph(string text) => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>";
        using var buffer = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            void Write(string name, string content)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Write("[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            Write("word/document.xml", $"<?xml version=\"1.0\"?><w:document {ns}><w:body>{Paragraph($"{SentinelFirstName} {SentinelLastName}")}{Paragraph(SentinelEmail)}{Paragraph("611 98 76 54")}</w:body></w:document>");
        }
        return buffer.ToArray();
    }

    // ---------------------------------------------------------------- helpers

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string fileName, byte[] content, string? token = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = form };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await client.SendAsync(request);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("code").GetString();
    }

    private static async Task<string?> FieldCodeAsync(HttpResponseMessage response)
    {
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    private async Task AssertNothingLeftBehindAsync(int expectedAudits)
    {
        await using var db = NewDbContext();
        Assert.False(await db.Candidates.AnyAsync());
        Assert.False(await db.Documents.AnyAsync());
        Assert.False(await db.Operations.AnyAsync());
        Assert.Equal(expectedAudits, await db.AuditEvents.CountAsync(row => row.EventType == CandidateAuditEvents.CvDraftExtracted));
        Assert.True(
            !Directory.Exists(_storageRoot) || !Directory.EnumerateFiles(_storageRoot, "*", SearchOption.AllDirectories).Any(),
            "A CV draft must leave no stored object.");
    }

    private static async Task<string> MintTokenAsync(HttpClient client, string subject, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/dev/token", new { subject, displayName = "Integration Caller", email });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DevelopmentTokenResponse>())!.AccessToken;
    }

    private async Task ResetAsync()
    {
        await using var dbContext = NewDbContext();
        await dbContext.Database.EnsureDeletedAsync();
        await DatabaseInitializer.MigrateAsync(dbContext, CancellationToken.None);
        await DatabaseInitializer.SeedCatalogsAsync(dbContext, CancellationToken.None);
        dbContext.Users.Add(new User(
            DraftTestActor.CreatorUserId,
            "draft-integration-actor",
            "Draft Integration User",
            "draft-integration@example.test",
            "rrhh_user",
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private async Task AddUserAsync(string subject, string email, string roleName)
    {
        await using var dbContext = NewDbContext();
        dbContext.Users.Add(new User(Guid.CreateVersion7(), subject, "Integration User", email, roleName, DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private ApplicationDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString).UseTestFieldEncryption().Options);

    private void SetSharedEnvironment(int? maxConcurrent)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ApplicationDatabase", database.ConnectionString);
        Environment.SetEnvironmentVariable("DocumentStorage__Root", _storageRoot);
        Environment.SetEnvironmentVariable("OperationWorker__Enabled", "false");
        Environment.SetEnvironmentVariable("CvDraft__MaxConcurrent", maxConcurrent?.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private WebApplicationFactory<Program> CreateActorFactory(
        ICurrentActor actor,
        IMalwareScanner? scanner = null,
        ICvTextReader? reader = null,
        LogCapture? loggerCapture = null,
        int? maxConcurrent = null)
    {
        SetSharedEnvironment(maxConcurrent);
        Environment.SetEnvironmentVariable("DevelopmentActor__Enabled", "true");
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DevelopmentActor:Enabled"] = "true",
                    ["ConnectionStrings:ApplicationDatabase"] = database.ConnectionString,
                    ["ProxyTrust:KnownNetworks:0"] = "127.0.0.0/8",
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICurrentActor>();
                services.AddScoped(_ => actor);
                ReplaceScanning(services, scanner, reader);
                if (loggerCapture is not null)
                {
                    // Serilog owns the logger factory; route every ILogger<T> through the same
                    // redaction enricher production uses and into the capture.
                    services.RemoveAll<ILoggerFactory>();
                    services.AddSingleton<ILoggerFactory>(_ => new Serilog.Extensions.Logging.SerilogLoggerFactory(
                        new Serilog.LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .Enrich.With<KeplerTalento.Web.Observability.PersonalDataRedactionEnricher>()
                            .WriteTo.Sink(loggerCapture)
                            .CreateLogger(),
                        dispose: true));
                }
            });
        });
    }

    private WebApplicationFactory<Program> CreateTokenFactory(IMalwareScanner? scanner, ICvTextReader? reader)
    {
        SetSharedEnvironment(null);
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
            builder.ConfigureTestServices(services => ReplaceScanning(services, scanner, reader));
        });
    }

    private static void ReplaceScanning(IServiceCollection services, IMalwareScanner? scanner, ICvTextReader? reader)
    {
        services.RemoveAll<IMalwareScanner>();
        services.AddSingleton(scanner ?? new CountingScanner());
        if (reader is not null)
        {
            services.RemoveAll<ICvTextReader>();
            services.AddSingleton(reader);
        }
    }

    private sealed record DevelopmentTokenResponse(string AccessToken);

    /// <summary>The marker scanner, counting its calls; or a fixed verdict when one is given.</summary>
    private sealed class CountingScanner(ScanResult? verdict = null) : IMalwareScanner
    {
        private readonly MarkerMalwareScanner _marker = new(Marker);
        private int _scans;

        public int Scans => _scans;

        public async Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _scans);
            return verdict ?? await _marker.ScanAsync(content, cancellationToken);
        }
    }

    /// <summary>The real reader, counting its calls and optionally holding them until released.</summary>
    private sealed class CountingReader(bool blockUntilReleased = false) : ICvTextReader
    {
        private readonly KeplerTalento.Infrastructure.CvExtraction.CvTextReader _inner = new(new(), new(), new CvDraftOptions());
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;

        public int Reads => _reads;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        public async Task<CvText> ReadAsync(CvFileKind kind, Stream content, CvReadBounds bounds, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _reads);
            Started.TrySetResult();
            if (blockUntilReleased)
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            return await _inner.ReadAsync(kind, content, bounds, cancellationToken);
        }
    }

    private sealed class DraftTestActor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static readonly Guid CreatorUserId = Guid.Parse("01932f00-0000-7000-8000-00000000d032");

        public static DraftTestActor Creator => new(true, Permissions.CandidatesCreate, Permissions.CandidatesRead);

        public string? ExternalKey => authenticated ? "draft-integration-actor" : null;

        public Guid? UserId => authenticated ? CreatorUserId : null;

        public bool IsAuthenticated => authenticated;

        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }

    private sealed class LogCapture : Serilog.Core.ILogEventSink
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

        public string Rendered() => string.Join('\n', _lines);

        public void Emit(Serilog.Events.LogEvent logEvent)
        {
            var properties = string.Join(' ', logEvent.Properties.Select(property => $"{property.Key}={property.Value}"));
            _lines.Enqueue($"{logEvent.RenderMessage()} {properties} {logEvent.Exception}");
        }
    }
}
