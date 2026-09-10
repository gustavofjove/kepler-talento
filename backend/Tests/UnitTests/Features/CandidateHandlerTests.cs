using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Catalogs;
using KeplerTalento.Domain.Documents;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

public sealed class CandidateHandlerTests
{
    [Fact]
    public async Task Create_stores_consent_metadata_exactly_as_supplied()
    {
        var candidates = new StubCandidateRepository();
        var handler = new CreateCandidateHandler(candidates, Catalogs(), Actor.Editor);

        var created = await handler.Handle(
            Draft() with { ReceivedAt = "2026-05-10", ConsentAt = "2026-05-11", ReviewDueAt = "2027-05-10" },
            CancellationToken.None);

        Assert.Equal("2026-05-10", created.ReceivedAt);
        Assert.Equal("2026-05-11", created.ConsentAt);
        Assert.Equal("2027-05-10", created.ReviewDueAt);
    }

    [Fact]
    public async Task Create_leaves_an_absent_consent_date_absent()
    {
        var candidates = new StubCandidateRepository();
        var handler = new CreateCandidateHandler(candidates, Catalogs(), Actor.Editor);

        var created = await handler.Handle(
            Draft() with { ReceivedAt = "2026-05-10", ConsentAt = null, ReviewDueAt = "" },
            CancellationToken.None);

        // Not today's date, not the received date — absent.
        Assert.Equal(string.Empty, created.ConsentAt);
        Assert.Equal(string.Empty, created.ReviewDueAt);
        Assert.Null(candidates.Single().ConsentAt);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("available")]
    [InlineData("in_process")]
    [InlineData("hired")]
    [InlineData("rejected")]
    public void Every_permitted_status_is_accepted(string status)
    {
        var result = new CreateCandidateValidator().Validate(Draft() with { Status = status });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("archived")]
    [InlineData("NEW")]
    [InlineData("")]
    public void A_status_outside_the_permitted_set_is_refused(string status)
    {
        var result = new CreateCandidateValidator().Validate(Draft() with { Status = status });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == CandidateErrors.StatusInvalid);
    }

    [Fact]
    public void Required_identity_is_validated_before_storage()
    {
        var result = new CreateCandidateValidator().Validate(
            Draft() with { FirstName = "  ", LastName = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorCode == CandidateErrors.FirstNameRequired);
        Assert.Contains(result.Errors, error => error.ErrorCode == CandidateErrors.LastNameRequired);
    }

    [Fact]
    public async Task An_update_that_omits_metadata_leaves_it_untouched()
    {
        var candidates = new StubCandidateRepository();
        var stored = Candidate("Ana", "Lopez");
        stored.SetConsent(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2),
            new DateOnly(2027, 1, 1),
            DateTimeOffset.UtcNow);
        candidates.Seed(stored);
        var handler = new UpdateCandidateHandler(candidates, Catalogs(), Actor.Editor);

        // Only the phone changes; the three metadata fields are simply not supplied.
        var updated = await handler.Handle(
            new UpdateCandidateCommand(
                stored.Id, "Ana", "Lopez", "+34 600 111 222", "", "", "", "España",
                "Inmediata", CandidateStatuses.New, "Email", "", null, null, null, stored.Version),
            CancellationToken.None);

        Assert.Equal("2026-01-01", updated.ReceivedAt);
        Assert.Equal("2026-01-02", updated.ConsentAt);
        Assert.Equal("2027-01-01", updated.ReviewDueAt);
    }

    [Fact]
    public async Task An_update_can_clear_a_metadata_date_explicitly()
    {
        var candidates = new StubCandidateRepository();
        var stored = Candidate("Ana", "Lopez");
        stored.SetConsent(null, new DateOnly(2026, 1, 2), null, DateTimeOffset.UtcNow);
        candidates.Seed(stored);
        var handler = new UpdateCandidateHandler(candidates, Catalogs(), Actor.Editor);

        var updated = await handler.Handle(
            new UpdateCandidateCommand(
                stored.Id, "Ana", "Lopez", "", "", "", "", "España",
                "Inmediata", CandidateStatuses.New, "Email", "", null, "", null, stored.Version),
            CancellationToken.None);

        Assert.Equal(string.Empty, updated.ConsentAt);
    }

    [Fact]
    public async Task A_version_conflict_is_refused_and_stores_no_audit_event()
    {
        var candidates = new StubCandidateRepository { NextOutcome = CandidateSaveOutcome.ConcurrencyConflict };
        candidates.Seed(Candidate("Ana", "Lopez"));
        var handler = new UpdateCandidateHandler(candidates, Catalogs(), Actor.Editor);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            UpdateOf(candidates.Single().Id, version: 99),
            CancellationToken.None));

        Assert.Equal(CandidateErrors.ConcurrencyConflict, refusal.Code);
        Assert.Null(candidates.LastAuditEventType);
    }

    [Fact]
    public async Task Removal_and_restoration_both_require_the_removal_capability()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var id = candidates.Single().Id;
        // An actor that may update but not remove.
        var handler = new SetCandidateActiveHandler(candidates, Catalogs(), Actor.Editor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SetCandidateActiveCommand(id, false, 0), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SetCandidateActiveCommand(id, true, 0), CancellationToken.None));
    }

    [Fact]
    public async Task Removal_records_a_deletion_timestamp_and_preserves_the_record()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var candidate = candidates.Single();
        var handler = new SetCandidateActiveHandler(candidates, Catalogs(), Actor.Remover);

        var removed = await handler.Handle(
            new SetCandidateActiveCommand(candidate.Id, false, candidate.Version),
            CancellationToken.None);

        Assert.False(removed.IsActive);
        Assert.NotNull(candidate.DeletedAtUtc);
        Assert.Equal(CandidateAuditEvents.Removed, candidates.LastAuditEventType);
        // Nothing was destroyed: the record is still there to be restored.
        Assert.Equal("Ana", candidate.FirstName);
    }

    [Fact]
    public async Task Restoring_a_removed_candidate_clears_the_deletion_timestamp()
    {
        var candidates = new StubCandidateRepository();
        var candidate = Candidate("Ana", "Lopez");
        candidate.Deactivate(DateTimeOffset.UtcNow);
        candidates.Seed(candidate);
        var handler = new SetCandidateActiveHandler(candidates, Catalogs(), Actor.Remover);

        var restored = await handler.Handle(
            new SetCandidateActiveCommand(candidate.Id, true, candidate.Version),
            CancellationToken.None);

        Assert.True(restored.IsActive);
        Assert.Null(candidate.DeletedAtUtc);
        Assert.Equal(CandidateAuditEvents.Restored, candidates.LastAuditEventType);
    }

    [Fact]
    public async Task A_candidate_already_in_the_requested_state_is_not_audited_as_changed()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var candidate = candidates.Single();
        var handler = new SetCandidateActiveHandler(candidates, Catalogs(), Actor.Remover);

        await handler.Handle(
            new SetCandidateActiveCommand(candidate.Id, true, candidate.Version),
            CancellationToken.None);

        // An audit event must describe a change that happened, not a request that arrived.
        Assert.Null(candidates.LastAuditEventType);
    }

    [Fact]
    public async Task A_relation_collection_is_replaced_as_a_whole_set()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var candidate = candidates.Single();
        var handler = new SetCandidateLanguagesHandler(candidates, Catalogs(), Actor.Editor);

        await handler.Handle(
            new SetCandidateLanguagesCommand(
                candidate.Id,
                [new CandidateLanguageInput(null, "Inglés", "B2", "Cambridge", null)],
                candidate.Version),
            CancellationToken.None);
        var afterFirst = candidate.Languages.Single().Id;

        var replaced = await handler.Handle(
            new SetCandidateLanguagesCommand(
                candidate.Id,
                [new CandidateLanguageInput(null, "Francés", "B1", null, null)],
                candidate.Version),
            CancellationToken.None);

        Assert.Equal("Francés", Assert.Single(replaced.Languages).Language);
        // The record that left the collection is deleted, not orphaned.
        Assert.Contains(candidates.RemovedRelations, relation => relation.Id == afterFirst);
    }

    [Fact]
    public async Task An_unresolvable_catalog_name_is_refused_and_never_created()
    {
        var catalogs = Catalogs();
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var candidate = candidates.Single();
        var before = (await catalogs.ListAsync(CatalogFamilies.Language, true, CancellationToken.None)).Count;
        var handler = new SetCandidateLanguagesHandler(candidates, catalogs, Actor.Editor);

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(() => handler.Handle(
            new SetCandidateLanguagesCommand(
                candidate.Id,
                [new CandidateLanguageInput(null, "Klingon", "B2", null, null)],
                candidate.Version),
            CancellationToken.None));

        Assert.Contains(refusal.Issues, issue => issue.Code == CandidateErrors.CatalogValueUnknown);
        // A typo must not permanently pollute the shared vocabulary.
        Assert.Equal(before, (await catalogs.ListAsync(CatalogFamilies.Language, true, CancellationToken.None)).Count);
        // The previous collection is untouched.
        Assert.Empty(candidate.Languages);
    }

    [Fact]
    public async Task Relation_duplicates_use_the_existing_specific_spanish_messages()
    {
        var languageCandidates = new StubCandidateRepository();
        languageCandidates.Seed(Candidate("Ana", "Lopez"));
        var language = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SetCandidateLanguagesHandler(languageCandidates, Catalogs(), Actor.Editor).Handle(
                new SetCandidateLanguagesCommand(
                    languageCandidates.Single().Id,
                    [
                        new CandidateLanguageInput(null, "English", "B2", null, null),
                        new CandidateLanguageInput(null, "  english  ", "B1", null, null),
                    ],
                    languageCandidates.Single().Version),
                CancellationToken.None));
        Assert.Contains(language.Issues, issue =>
            issue.Code == CandidateErrors.LanguageDuplicate &&
            issue.Message == "El candidato ya tiene este idioma registrado.");

        var programCandidates = new StubCandidateRepository();
        programCandidates.Seed(Candidate("Bea", "Mora"));
        var program = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SetCandidateProgramsHandler(programCandidates, Catalogs(), Actor.Editor).Handle(
                new SetCandidateProgramsCommand(
                    programCandidates.Single().Id,
                    [
                        new CandidateProgramInput(null, "Excel", "Avanzado", null, null),
                        new CandidateProgramInput(null, " excel ", "Avanzado", null, null),
                    ],
                    programCandidates.Single().Version),
                CancellationToken.None));
        Assert.Contains(program.Issues, issue =>
            issue.Code == CandidateErrors.ProgramDuplicate &&
            issue.Message == "El candidato ya tiene este programa registrado.");

        var skillCandidates = new StubCandidateRepository();
        skillCandidates.Seed(Candidate("Carla", "Gil"));
        var skill = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SetCandidateSkillsHandler(skillCandidates, Catalogs(), Actor.Editor).Handle(
                new SetCandidateSkillsCommand(
                    skillCandidates.Single().Id,
                    [
                        new CandidateSkillInput(null, "Compras", "Alto", null),
                        new CandidateSkillInput(null, " compras ", "Alto", null),
                    ],
                    skillCandidates.Single().Version),
                CancellationToken.None));
        Assert.Contains(skill.Issues, issue =>
            issue.Code == CandidateErrors.SkillDuplicate &&
            issue.Message == "El candidato ya tiene esta habilidad registrada.");
    }

    [Fact]
    public async Task Two_degrees_from_the_same_institution_are_accepted()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Dora", "Sanz"));
        var response = await new SetCandidateEducationHandler(candidates, Catalogs(), Actor.Editor).Handle(
            new SetCandidateEducationCommand(
                candidates.Single().Id,
                [
                    new CandidateEducationInput(null, "Universitaria", "Grado A", null, "UCM", "Completa", 2020, null),
                    new CandidateEducationInput(null, "Universitaria", "Grado B", null, "UCM", "Completa", 2022, null),
                ],
                candidates.Single().Version),
            CancellationToken.None);
        Assert.Equal(2, response.Education.Count);
    }

    [Fact]
    public async Task A_relation_write_to_a_removed_candidate_is_refused()
    {
        var candidates = new StubCandidateRepository();
        var candidate = Candidate("Ana", "Lopez");
        candidate.Deactivate(DateTimeOffset.UtcNow);
        candidates.Seed(candidate);
        var handler = new SetCandidateSkillsHandler(candidates, Catalogs(), Actor.Editor);

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(() => handler.Handle(
            new SetCandidateSkillsCommand(
                candidate.Id,
                [new CandidateSkillInput(null, "Compras", "Alto", null)],
                candidate.Version),
            CancellationToken.None));

        Assert.Contains(refusal.Issues, issue => issue.Code == CandidateErrors.RemovedCandidate);
        Assert.Empty(candidate.Skills);
    }

    [Fact]
    public async Task Reading_returns_a_logically_removed_candidate_by_identifier()
    {
        var candidates = new StubCandidateRepository();
        var candidate = Candidate("Ana", "Lopez");
        candidate.Deactivate(DateTimeOffset.UtcNow);
        candidates.Seed(candidate);
        var handler = new GetCandidateHandler(candidates, Catalogs(), Actor.Reader);

        var found = await handler.Handle(new GetCandidateQuery(candidate.Id), CancellationToken.None);

        Assert.False(found.IsActive);
        Assert.Equal("Ana", found.FirstName);
    }

    [Fact]
    public async Task Listing_excludes_removed_candidates_unless_asked()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Activa", "Uno"));
        var removed = Candidate("Retirada", "Dos");
        removed.Deactivate(DateTimeOffset.UtcNow);
        candidates.Seed(removed);
        var handler = new ListCandidatesHandler(candidates, Actor.Reader);

        var byDefault = await handler.Handle(new ListCandidatesQuery(false), CancellationToken.None);
        var withRemoved = await handler.Handle(new ListCandidatesQuery(true), CancellationToken.None);

        Assert.Equal("Activa", Assert.Single(byDefault).FirstName);
        Assert.Equal(2, withRemoved.Count);
        Assert.Contains(withRemoved, summary => !summary.IsActive);
    }

    public static TheoryData<string> WriteCapabilities => new()
    {
        Permissions.CandidatesCreate,
        Permissions.CandidatesUpdate,
        Permissions.CandidatesDelete,
        Permissions.CandidatesRead,
    };

    [Theory]
    [MemberData(nameof(WriteCapabilities))]
    public async Task Every_capability_fails_closed_for_an_unauthenticated_actor(string capability)
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var id = candidates.Single().Id;
        // Holds the permission, but is not authenticated: both must be true.
        var actor = new Actor(false, capability);

        await Assert.ThrowsAsync<ForbiddenException>(() => Invoke(capability, candidates, actor, id));
    }

    [Theory]
    [MemberData(nameof(WriteCapabilities))]
    public async Task Every_capability_fails_closed_for_an_actor_lacking_it(string capability)
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var id = candidates.Single().Id;
        var actor = new Actor(true);

        await Assert.ThrowsAsync<ForbiddenException>(() => Invoke(capability, candidates, actor, id));
    }

    [Fact]
    public async Task A_refusal_does_not_disclose_whether_the_candidate_exists()
    {
        var candidates = new StubCandidateRepository();
        candidates.Seed(Candidate("Ana", "Lopez"));
        var existing = candidates.Single().Id;
        var handler = new GetCandidateHandler(candidates, Catalogs(), new Actor(true));

        var forExisting = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetCandidateQuery(existing), CancellationToken.None));
        var forMissing = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetCandidateQuery(Guid.NewGuid()), CancellationToken.None));

        // Identical refusals: the caller learns nothing about which identifiers are real.
        Assert.Equal(forExisting.Code, forMissing.Code);
        Assert.Equal(forExisting.Message, forMissing.Message);
    }

    private static Task Invoke(
        string capability,
        StubCandidateRepository candidates,
        ICurrentActor actor,
        Guid id) => capability switch
        {
            Permissions.CandidatesCreate => new CreateCandidateHandler(candidates, Catalogs(), actor)
                .Handle(Draft(), CancellationToken.None),
            Permissions.CandidatesUpdate => new UpdateCandidateHandler(candidates, Catalogs(), actor)
                .Handle(UpdateOf(id, 0), CancellationToken.None),
            Permissions.CandidatesDelete => new SetCandidateActiveHandler(candidates, Catalogs(), actor)
                .Handle(new SetCandidateActiveCommand(id, false, 0), CancellationToken.None),
            _ => new GetCandidateHandler(candidates, Catalogs(), actor)
                .Handle(new GetCandidateQuery(id), CancellationToken.None),
        };

    private static CreateCandidateCommand Draft() => new(
        "Ana", "Lopez", "", "", "", "", "España", "Inmediata",
        CandidateStatuses.New, "Email", "", null, null, null);

    private static UpdateCandidateCommand UpdateOf(Guid id, uint version) => new(
        id, "Ana", "Lopez", "", "", "", "", "España", "Inmediata",
        CandidateStatuses.New, "Email", "", null, null, null, version);

    private static Candidate Candidate(string firstName, string lastName) =>
        new(Guid.CreateVersion7(), firstName, lastName, DateTimeOffset.UtcNow);

    private static StubCatalogRepository Catalogs()
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var items = new List<CatalogItem>();
        void Add(string family, params string[] names)
        {
            for (var index = 0; index < names.Length; index++)
            {
                items.Add(new CatalogItem(
                    Guid.NewGuid(), family, CatalogName.DeriveCode(names[index]),
                    names[index], null, index + 1, createdAtUtc));
            }
        }
        Add(CatalogFamilies.Language, "Inglés", "Francés");
        Add(CatalogFamilies.LanguageLevel, "B1", "B2");
        Add(CatalogFamilies.Language, "English");
        Add(CatalogFamilies.Skill, "Compras");
        Add(CatalogFamilies.SkillLevel, "Alto");
        Add(CatalogFamilies.Program, "Excel");
        Add(CatalogFamilies.ProgramLevel, "Avanzado");
        Add(CatalogFamilies.EducationType, "Universitaria");
        Add(CatalogFamilies.EducationStatus, "Completa");
        return new StubCatalogRepository(items);
    }

    private sealed class StubCatalogRepository(List<CatalogItem> items) : ICatalogRepository
    {
        public Task<IReadOnlyList<CatalogItem>> ListAsync(
            string family,
            bool includeInactive,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>(items
                .Where(item => item.Family == family && (includeInactive || item.IsActive))
                .ToArray());

        public Task<CatalogItem?> FindAsync(string family, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(items.SingleOrDefault(item => item.Family == family && item.Id == id));

        public void Add(CatalogItem item) => items.Add(item);

        public void ExpectVersion(CatalogItem item, uint version) { }

        public Task<CatalogSaveOutcome> SaveAsync(
            string auditEventType,
            string subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CatalogSaveOutcome.Saved);
    }

    private sealed class StubCandidateRepository : ICandidateRepository
    {
        private readonly List<Candidate> _candidates = [];

        public List<CandidateRelation> RemovedRelations { get; } = [];
        public string? LastAuditEventType { get; private set; }
        public uint? ExpectedVersion { get; private set; }
        public CandidateSaveOutcome NextOutcome { get; set; } = CandidateSaveOutcome.Saved;

        public void Seed(Candidate candidate) => _candidates.Add(candidate);

        public Candidate Single() => _candidates[0];

        public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_candidates.SingleOrDefault(candidate => candidate.Id == id));

        public Task<Candidate?> FindCoreAsync(Guid id, CancellationToken cancellationToken) =>
            FindAsync(id, cancellationToken);

        public Task<IReadOnlyList<CandidateSummary>> ListAsync(
            bool includeInactive,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateSummary>>(_candidates
                .Where(candidate => includeInactive || candidate.IsActive)
                .Select(candidate => new CandidateSummary(
                    candidate.Id, candidate.FirstName, candidate.LastName, candidate.Phone,
                    candidate.Email, candidate.Location, candidate.Province, candidate.Country,
                    candidate.Availability, candidate.Status, candidate.Source, candidate.Notes,
                    candidate.ReceivedAt, candidate.ConsentAt, candidate.ReviewDueAt,
                    candidate.IsActive, candidate.CreatedAtUtc, candidate.UpdatedAtUtc,
                    candidate.Version, 0, null))
                .ToArray());

        public Task<IReadOnlyList<CandidateDocument>> ListDocumentsAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateDocument>>([]);

        /// <summary>
        /// Records what the search handler asked for. The query semantics themselves are
        /// SQL, so they are proven against PostgreSQL rather than reimplemented here —
        /// what these tests can prove is that authorization, normalization and the page
        /// bounds happen before the repository is reached, and with which values.
        /// </summary>
        public SearchFiltersValue? LastSearchFilters { get; private set; }
        public (int Page, int PageSize)? LastSearchPaging { get; private set; }
        public SearchPage<CandidateSearchItem> NextSearchPage { get; set; } =
            new([], SearchPaging.DefaultPage, SearchPaging.DefaultPageSize, 0);

        public Task<SearchPage<CandidateSearchItem>> SearchAsync(
            SearchFiltersValue filters,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastSearchFilters = filters;
            LastSearchPaging = (page, pageSize);
            return Task.FromResult(NextSearchPage);
        }

        public void Add(Candidate candidate) => _candidates.Add(candidate);

        public List<CandidateRelation> AddedRelations { get; } = [];

        public void AddRelation(CandidateRelation relation) => AddedRelations.Add(relation);

        public void RemoveRelations(IEnumerable<CandidateRelation> relations) =>
            RemovedRelations.AddRange(relations);

        public Task<CandidateSaveOutcome> ReplaceDocumentsAsync(
            Candidate candidate,
            uint version,
            IReadOnlyList<DocumentMetadata> desired,
            string auditEventType,
            CancellationToken cancellationToken)
        {
            ExpectedVersion = version;
            if (NextOutcome == CandidateSaveOutcome.Saved)
            {
                LastAuditEventType = auditEventType;
            }
            return Task.FromResult(NextOutcome);
        }

        public void ExpectVersion(Candidate candidate, uint version) => ExpectedVersion = version;

        public Task<CandidateSaveOutcome> SaveAsync(
            string auditEventType,
            string subjectId,
            CancellationToken cancellationToken)
        {
            if (NextOutcome == CandidateSaveOutcome.Saved)
            {
                LastAuditEventType = auditEventType;
            }
            return Task.FromResult(NextOutcome);
        }
    }

    private sealed class Actor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static Actor Reader => new(true, Permissions.CandidatesRead);
        public static Actor Editor =>
            new(true, Permissions.CandidatesRead, Permissions.CandidatesCreate, Permissions.CandidatesUpdate);
        public static Actor Remover =>
            new(true, Permissions.CandidatesRead, Permissions.CandidatesDelete);

        public string? ExternalKey => authenticated ? "test-actor" : null;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
