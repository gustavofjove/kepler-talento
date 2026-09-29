using KeplerTalento.Application.Abstractions.Correlation;
using KeplerTalento.Application.Abstractions.CvExtraction;
using KeplerTalento.Application.Abstractions.Documents;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates.CvDraft;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Candidates;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// KTL-32: the order of the gates (permission, format, scan, parse) and what an attempt leaves
/// behind — one audit row with ids and a code, nothing from the file.
/// </summary>
public sealed class CandidateDraftHandlerTests
{
    private const string SentinelName = "Zoraida Villalobos";
    private const string SentinelEmail = "zoraida.villalobos@sentinel.invalid";
    private const string SentinelFileName = "cv-zoraida-villalobos.pdf";

    [Fact]
    public async Task A_clean_cv_yields_only_the_suggested_fields()
    {
        var (handler, doubles) = Build();

        var response = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(CvDraftCodes.Extracted, response.Outcome);
        Assert.Equal(["email", "firstName", "lastName"], response.Fields.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new CandidateDraftFieldResponse("Zoraida", "high"), response.Fields["firstName"]);
        Assert.Equal(new CandidateDraftFieldResponse(SentinelEmail, "high"), response.Fields["email"]);
        Assert.Equal(1, doubles.Reader.Reads);
    }

    [Theory]
    [MemberData(nameof(RefusedActors))]
    public async Task An_actor_without_the_create_permission_is_refused_before_anything_is_read(ICurrentActor actor)
    {
        var (handler, doubles) = Build(actor: actor);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(0, doubles.Inspector.Inspections);
        Assert.Equal(0, doubles.Scanner.Scans);
        Assert.Equal(0, doubles.Reader.Reads);
        Assert.Empty(doubles.Audits.Events);
    }

    public static TheoryData<ICurrentActor> RefusedActors => new()
    {
        new Actor(false),
        new Actor(true, Permissions.CandidatesRead),
        new Actor(true, Permissions.DocumentsUpload, Permissions.CandidatesUpdate),
    };

    [Theory]
    [InlineData("cv.doc")]
    [InlineData("cv.odt")]
    [InlineData("cv.txt")]
    [InlineData("cv.png")]
    [InlineData("cv")]
    public async Task Formats_other_than_pdf_and_docx_are_refused_before_scanning(string fileName)
    {
        var (handler, doubles) = Build();

        var failure = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(Command(fileName), CancellationToken.None));

        Assert.Equal(CvDraftCodes.FormatUnsupported, failure.Issues.Single().Code);
        Assert.Equal(0, doubles.Scanner.Scans);
        Assert.Equal(0, doubles.Reader.Reads);
    }

    [Fact]
    public async Task Content_the_inspector_rejects_is_refused_before_scanning()
    {
        var (handler, doubles) = Build(inspection: new InspectedDocument(false, "document.content.mismatch_or_unsafe", "application/pdf"));

        var failure = await Assert.ThrowsAsync<RequestValidationException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(CvDraftCodes.ContentRejected, failure.Issues.Single().Code);
        Assert.Equal(0, doubles.Scanner.Scans);
        Assert.Equal(0, doubles.Reader.Reads);
    }

    [Fact]
    public async Task Infected_content_is_never_parsed_and_the_signature_is_not_disclosed()
    {
        var (handler, doubles) = Build(scan: new ScanResult(ScanVerdict.Infected, "scanner.infected", "Sentinel.Signature-123"));

        var failure = await Assert.ThrowsAsync<UnprocessableException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(CvDraftCodes.Rejected, failure.Code);
        Assert.DoesNotContain("Sentinel.Signature", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, doubles.Reader.Reads);
    }

    [Theory]
    [InlineData("scanner.unavailable")]
    [InlineData("scanner.timeout")]
    [InlineData("scanner.error")]
    public async Task A_scanner_that_cannot_judge_fails_closed(string code)
    {
        var (handler, doubles) = Build(scan: new ScanResult(ScanVerdict.Error, code));

        var failure = await Assert.ThrowsAsync<ServiceUnavailableException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(CvDraftCodes.ScannerUnavailable, failure.Code);
        Assert.Equal(0, doubles.Reader.Reads);
    }

    [Fact]
    public async Task Unreadable_content_is_refused_with_the_generic_message()
    {
        var (handler, _) = Build(reader: new StubReader(throws: new CvUnreadableException()));

        var failure = await Assert.ThrowsAsync<UnprocessableException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(CvDraftCodes.Unreadable, failure.Code);
        Assert.Equal(CvDraftCodes.NotProcessableMessage, failure.Message);
    }

    [Fact]
    public async Task A_read_over_the_time_budget_is_refused_as_too_complex()
    {
        var (handler, _) = Build(reader: new StubReader(hangs: true), options: new CvDraftOptions { TimeBudgetSeconds = 1 });

        var failure = await Assert.ThrowsAsync<UnprocessableException>(() => handler.Handle(Command(), CancellationToken.None));

        Assert.Equal(CvDraftCodes.TooComplex, failure.Code);
    }

    [Fact]
    public async Task A_cv_without_text_yields_an_empty_draft()
    {
        var (handler, _) = Build(reader: new StubReader(new CvText([new CvLine(0, "   ")], 1)));

        var response = await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(CvDraftCodes.NoText, response.Outcome);
        Assert.Empty(response.Fields);
    }

    [Fact]
    public async Task The_parsers_receive_the_bounds_from_configuration()
    {
        var (handler, doubles) = Build(options: new CvDraftOptions { MaxPdfPages = 2, MaxCharacters = 5_000 });

        await handler.Handle(Command(), CancellationToken.None);

        Assert.Equal(new CvReadBounds(2, 5_000), doubles.Reader.LastBounds);
    }

    [Theory]
    [InlineData("cv.pdf", CvFileKind.Pdf)]
    [InlineData("CV.PDF", CvFileKind.Pdf)]
    [InlineData("cv.docx", CvFileKind.Docx)]
    public async Task The_reader_is_chosen_by_extension(string fileName, CvFileKind kind)
    {
        var (handler, doubles) = Build();

        await handler.Handle(Command(fileName), CancellationToken.None);

        Assert.Equal(kind, doubles.Reader.LastKind);
    }

    [Fact]
    public async Task Every_attempt_records_one_audit_event_with_ids_and_a_code_only()
    {
        var (handler, doubles) = Build();
        var command = Command();

        await handler.Handle(command, CancellationToken.None);

        var audit = Assert.Single(doubles.Audits.Events);
        Assert.Equal(CandidateAuditEvents.CvDraftExtracted, audit.EventType);
        Assert.Equal(command.DraftId.ToString("N"), audit.SubjectId);
        Assert.Equal(CvDraftCodes.Extracted, audit.OutcomeCode);
        Assert.Equal(Actor.StoredUserId, audit.ActorUserId);
        Assert.Equal("correlation-ktl-32", audit.CorrelationId);
        AssertCarriesNothingFromTheFile(audit);
    }

    [Theory]
    [MemberData(nameof(RefusalOutcomes))]
    public async Task A_refused_attempt_is_audited_with_its_refusal_code(string scenario, string expectedCode)
    {
        var (handler, doubles) = scenario switch
        {
            "format" => Build(),
            "infected" => Build(scan: new ScanResult(ScanVerdict.Infected, "scanner.infected", "sig")),
            "scanner" => Build(scan: new ScanResult(ScanVerdict.Error, "scanner.unavailable")),
            _ => Build(reader: new StubReader(throws: new CvUnreadableException())),
        };

        await Assert.ThrowsAnyAsync<ApplicationExceptionBase>(
            () => handler.Handle(Command(scenario == "format" ? "cv.txt" : SentinelFileName), CancellationToken.None));

        var audit = Assert.Single(doubles.Audits.Events);
        Assert.Equal(expectedCode, audit.OutcomeCode);
        AssertCarriesNothingFromTheFile(audit);
    }

    public static TheoryData<string, string> RefusalOutcomes => new()
    {
        { "format", CvDraftCodes.FormatUnsupported },
        { "infected", CvDraftCodes.Rejected },
        { "scanner", CvDraftCodes.ScannerUnavailable },
        { "unreadable", CvDraftCodes.Unreadable },
    };

    private static void AssertCarriesNothingFromTheFile(AuditEvent audit)
    {
        var recorded = $"{audit.EventType}|{audit.SubjectId}|{audit.OutcomeCode}|{audit.CorrelationId}";
        Assert.DoesNotContain("Zoraida", recorded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("villalobos", recorded, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".pdf", recorded, StringComparison.OrdinalIgnoreCase);
    }

    private static CreateCandidateDraftCommand Command(string fileName = SentinelFileName) =>
        new(Guid.CreateVersion7(), fileName, new MemoryStream("%PDF-1.7 synthetic"u8.ToArray()));

    private sealed record Doubles(StubInspector Inspector, StubScanner Scanner, StubReader Reader, RecordingAudits Audits);

    private static (CreateCandidateDraftHandler Handler, Doubles Doubles) Build(
        ICurrentActor? actor = null,
        InspectedDocument? inspection = null,
        ScanResult? scan = null,
        StubReader? reader = null,
        CvDraftOptions? options = null)
    {
        var doubles = new Doubles(
            new StubInspector(inspection ?? new InspectedDocument(true, "document.accepted", "application/pdf")),
            new StubScanner(scan ?? new ScanResult(ScanVerdict.Clean, "scanner.clean")),
            reader ?? new StubReader(new CvText([new CvLine(0, SentinelName, 24), new CvLine(0, SentinelEmail, 10)], 1)),
            new RecordingAudits());
        var handler = new CreateCandidateDraftHandler(
            doubles.Inspector,
            doubles.Scanner,
            doubles.Reader,
            new StubExtractor(),
            options ?? new CvDraftOptions(),
            doubles.Audits,
            new StubCorrelation(),
            actor ?? Actor.Creator);
        return (handler, doubles);
    }

    private sealed class StubInspector(InspectedDocument result) : IDocumentContentInspector
    {
        public int Inspections { get; private set; }

        public Task<InspectedDocument> InspectAsync(string fileName, Stream content, CancellationToken cancellationToken)
        {
            Inspections++;
            return Task.FromResult(result);
        }
    }

    private sealed class StubScanner(ScanResult result) : IMalwareScanner
    {
        public int Scans { get; private set; }

        public Task<ScanResult> ScanAsync(Stream content, CancellationToken cancellationToken)
        {
            Scans++;
            return Task.FromResult(result);
        }
    }

    private sealed class StubReader(CvText? text = null, Exception? throws = null, bool hangs = false) : ICvTextReader
    {
        public int Reads { get; private set; }

        public CvFileKind? LastKind { get; private set; }

        public CvReadBounds? LastBounds { get; private set; }

        public async Task<CvText> ReadAsync(CvFileKind kind, Stream content, CvReadBounds bounds, CancellationToken cancellationToken)
        {
            Reads++;
            LastKind = kind;
            LastBounds = bounds;
            if (hangs)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            if (throws is not null)
            {
                throw throws;
            }
            return text ?? CvText.Empty;
        }
    }

    /// <summary>Echoes the first two lines as a name and an e-mail, so the handler's mapping is visible.</summary>
    private sealed class StubExtractor : ICandidateDraftExtractor
    {
        public CandidateDraftSuggestions Extract(CvText text)
        {
            var name = text.Lines[0].Text.Split(' ');
            return new CandidateDraftSuggestions(
                FirstName: new FieldSuggestion(name[0], SuggestionConfidence.High),
                LastName: new FieldSuggestion(name[1], SuggestionConfidence.Low),
                Email: new FieldSuggestion(text.Lines[1].Text, SuggestionConfidence.High),
                Phone: new FieldSuggestion("   ", SuggestionConfidence.Low));
        }
    }

    private sealed class RecordingAudits : IAuditRepository
    {
        public List<AuditEvent> Events { get; } = [];

        public Task RecordAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }

        public Task<AuditPage> ListAsync(AuditFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubCorrelation : ICorrelationContext
    {
        public string CorrelationId => "correlation-ktl-32";
    }

    public sealed class Actor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static readonly Guid StoredUserId = Guid.Parse("01932f00-0000-7000-8000-00000000c032");

        public static Actor Creator => new(true, Permissions.CandidatesCreate);

        public string? ExternalKey => authenticated ? "draft-actor" : null;

        public Guid? UserId => authenticated ? StoredUserId : null;

        public bool IsAuthenticated => authenticated;

        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);

        public override string ToString() => $"authenticated={authenticated} [{string.Join(',', permissions)}]";
    }
}
