using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Search;
using KeplerTalento.Domain.Candidates;
using KeplerTalento.Domain.Documents;
using KeplerTalento.Domain.Search;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

/// <summary>
/// The search and saved-search handlers. What is proven here is everything that happens
/// before and around the query — authorization, normalization, page bounds, versions and
/// refusals. The query's own semantics are SQL, and are proven against PostgreSQL.
/// </summary>
public sealed class SearchHandlerTests
{
    [Fact]
    public async Task Search_refuses_an_unauthenticated_caller_before_reaching_the_repository()
    {
        var candidates = new RecordingCandidateRepository();
        var handler = new SearchCandidatesHandler(candidates, Actor.Anonymous);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None));
        Assert.Null(candidates.LastPaging);
    }

    [Fact]
    public async Task Search_refuses_an_actor_without_candidate_read_before_reaching_the_repository()
    {
        var candidates = new RecordingCandidateRepository();
        var handler = new SearchCandidatesHandler(candidates, Actor.WithoutPermissions);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None));
        Assert.Null(candidates.LastPaging);
    }

    [Fact]
    public async Task Search_applies_the_documented_defaults_when_pagination_is_absent()
    {
        var candidates = new RecordingCandidateRepository();
        var handler = new SearchCandidatesHandler(candidates, Actor.Reader);

        var page = await handler.Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None);

        Assert.Equal((1, 25), candidates.LastPaging);
        Assert.Equal(1, page.Page);
        Assert.Equal(25, page.PageSize);
    }

    [Fact]
    public async Task Search_refuses_out_of_range_pagination_before_querying()
    {
        var candidates = new RecordingCandidateRepository();
        var handler = new SearchCandidatesHandler(candidates, Actor.Reader);

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            handler.Handle(new SearchCandidatesQuery(null, 1, 500), CancellationToken.None));
        // The point of the bound is that the unbounded query never runs, not that its result
        // is discarded afterwards.
        Assert.Null(candidates.LastPaging);
    }

    [Fact]
    public async Task Search_defaults_to_update_time_descending_and_active_candidates_only()
    {
        var candidates = new RecordingCandidateRepository();

        await new SearchCandidatesHandler(candidates, Actor.Reader)
            .Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None);

        Assert.Equal(SearchSort.Default, candidates.LastOptions!.Sort);
        Assert.False(candidates.LastOptions.IncludeInactive);
    }

    [Theory]
    [InlineData("updatedAt", "asc", SearchSortField.UpdatedAt, SearchSortDirection.Ascending)]
    [InlineData("updatedAt", "desc", SearchSortField.UpdatedAt, SearchSortDirection.Descending)]
    [InlineData("lastName", "asc", SearchSortField.LastName, SearchSortDirection.Ascending)]
    [InlineData("lastName", "desc", SearchSortField.LastName, SearchSortDirection.Descending)]
    [InlineData("status", "asc", SearchSortField.Status, SearchSortDirection.Ascending)]
    [InlineData("status", "desc", SearchSortField.Status, SearchSortDirection.Descending)]
    public async Task Each_documented_sort_field_is_accepted_in_both_directions(
        string field,
        string direction,
        SearchSortField expectedField,
        SearchSortDirection expectedDirection)
    {
        var candidates = new RecordingCandidateRepository();

        await new SearchCandidatesHandler(candidates, Actor.Reader)
            .Handle(new SearchCandidatesQuery(null, null, null, false, field, direction), CancellationToken.None);

        Assert.Equal(new SearchSort(expectedField, expectedDirection), candidates.LastOptions!.Sort);
    }

    [Theory]
    [InlineData("email", null, SearchErrors.SortFieldInvalid)]
    [InlineData("UpdatedAtUtc\"; DROP TABLE \"CND_Candidates\"; --", null, SearchErrors.SortFieldInvalid)]
    [InlineData("LASTNAME", null, SearchErrors.SortFieldInvalid)]
    [InlineData(null, "sideways", SearchErrors.SortDirectionInvalid)]
    public async Task An_unknown_sort_is_refused_with_a_stable_code_before_querying(
        string? field,
        string? direction,
        string code)
    {
        var candidates = new RecordingCandidateRepository();

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SearchCandidatesHandler(candidates, Actor.Reader)
                .Handle(new SearchCandidatesQuery(null, null, null, false, field, direction), CancellationToken.None));

        Assert.Contains(refusal.Issues, issue => issue.Code == code);
        Assert.Null(candidates.LastOptions);
    }

    [Fact]
    public async Task Pagination_bounds_are_unchanged_by_sorting()
    {
        var candidates = new RecordingCandidateRepository();

        var refusal = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new SearchCandidatesHandler(candidates, Actor.Reader)
                .Handle(new SearchCandidatesQuery(null, 0, 101, false, "status", "asc"), CancellationToken.None));

        Assert.Contains(refusal.Issues, issue => issue.Code == SearchErrors.PageInvalid);
        Assert.Contains(refusal.Issues, issue => issue.Code == SearchErrors.PageSizeInvalid);
    }

    [Fact]
    public async Task Including_removed_candidates_without_the_removal_permission_is_forbidden_before_validation()
    {
        var candidates = new RecordingCandidateRepository();

        // Malformed on purpose: the refusal must be Forbidden, not a validation problem.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new SearchCandidatesHandler(candidates, Actor.Reader)
                .Handle(new SearchCandidatesQuery(null, 0, 500, true, "nope", "nope"), CancellationToken.None));
        Assert.Null(candidates.LastOptions);
    }

    [Fact]
    public async Task Including_removed_candidates_reaches_the_repository_for_a_permitted_actor()
    {
        var candidates = new RecordingCandidateRepository();

        await new SearchCandidatesHandler(candidates, Actor.Remover)
            .Handle(new SearchCandidatesQuery(null, null, null, true), CancellationToken.None);

        Assert.True(candidates.LastOptions!.IncludeInactive);
    }

    [Fact]
    public async Task Search_hands_the_repository_a_normalized_filter_value()
    {
        var candidates = new RecordingCandidateRepository();
        var handler = new SearchCandidatesHandler(candidates, Actor.Reader);

        await handler.Handle(
            new SearchCandidatesQuery(
                new SearchFiltersInput(
                    "  Marta  ",
                    null,
                    [new SearchCriterionInput("Java", ""), new SearchCriterionInput(" java ", " ")],
                    "all",
                    null,
                    null,
                    null,
                    null,
                    null),
                null,
                null),
            CancellationToken.None);

        Assert.Equal("Marta", candidates.LastFilters!.Text);
        Assert.Single(candidates.LastFilters.SkillCriteria);
        Assert.Equal(MultiValueMode.All, candidates.LastFilters.SkillMode);
    }

    [Fact]
    public async Task Actors_differing_only_by_a_broader_read_permission_issue_the_same_query()
    {
        // KTL-10 defines no visibility scope, so there is nothing for a broader permission
        // to widen. This asserts the decision rather than assuming nobody implemented one.
        var narrow = new RecordingCandidateRepository();
        var broad = new RecordingCandidateRepository();

        await new SearchCandidatesHandler(narrow, Actor.Reader)
            .Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None);
        await new SearchCandidatesHandler(broad, Actor.ReaderWithViewAll)
            .Handle(new SearchCandidatesQuery(null, null, null), CancellationToken.None);

        Assert.Equal(
            SearchFilterDocument.Serialize(narrow.LastFilters!),
            SearchFilterDocument.Serialize(broad.LastFilters!));
        Assert.Equal(narrow.LastPaging, broad.LastPaging);
    }

    [Fact]
    public async Task Every_actor_that_can_read_lists_the_whole_shared_library()
    {
        var presets = new StubPresetRepository();
        var second = presets.Seed("Zeta");
        var first = presets.Seed("Alfa");

        var listed = await new ListSearchPresetsHandler(presets, Actor.Reader)
            .Handle(new ListSearchPresetsQuery(), CancellationToken.None);

        Assert.Equal([first.Id, second.Id], listed.Select(preset => preset.Id));
    }

    [Fact]
    public async Task A_single_preset_is_retrieved_with_its_version()
    {
        var presets = new StubPresetRepository();
        var seeded = presets.Seed("Java senior");

        var found = await new GetSearchPresetHandler(presets, Actor.Reader)
            .Handle(new GetSearchPresetQuery(seeded.Id), CancellationToken.None);

        Assert.Equal("Java senior", found.Name);
        Assert.Equal(1u, found.Version);
    }

    [Fact]
    public async Task An_unauthenticated_caller_is_refused_by_every_preset_handler()
    {
        var presets = new StubPresetRepository();
        var seeded = presets.Seed("Existente");

        await AssertEveryPresetHandlerRefuses(presets, seeded.Id, Actor.Anonymous, reads: true, writes: true);

        Assert.Equal(0, presets.SaveCount);
    }

    [Fact]
    public async Task A_reader_without_manage_presets_cannot_write_the_library()
    {
        var presets = new StubPresetRepository();
        var seeded = presets.Seed("Existente");

        await AssertEveryPresetHandlerRefuses(presets, seeded.Id, Actor.Reader, reads: false, writes: true);

        Assert.Equal(0, presets.SaveCount);
        Assert.Equal("Existente", presets.Single().Name);
    }

    [Fact]
    public async Task A_manager_without_candidate_read_cannot_list_retrieve_or_apply()
    {
        // Managing does not imply reading: the two permissions are independent by design.
        var presets = new StubPresetRepository();
        var seeded = presets.Seed("Existente");

        await AssertEveryPresetHandlerRefuses(presets, seeded.Id, Actor.ManagerOnly, reads: true, writes: false);

        Assert.Equal(0, presets.SaveCount);
        Assert.Null(presets.Single().LastUsedAtUtc);
    }

    [Fact]
    public async Task Creating_a_preset_stamps_server_timestamps_and_starts_at_version_one()
    {
        var presets = new StubPresetRepository();
        var before = DateTimeOffset.UtcNow;

        var created = await new CreateSearchPresetHandler(presets, Actor.Manager).Handle(
            new CreateSearchPresetCommand("  Java senior  ", null),
            CancellationToken.None);

        Assert.Equal("Java senior", created.Name);
        Assert.True(created.CreatedAt >= before);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Null(created.LastUsedAt);
        Assert.Equal(1u, created.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_preset_name_is_refused(string name)
    {
        var failure = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new CreateSearchPresetHandler(new StubPresetRepository(), Actor.Manager)
                .Handle(new CreateSearchPresetCommand(name, null), CancellationToken.None));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.PresetNameRequired);
    }

    [Fact]
    public async Task An_invalid_filter_is_refused_and_stores_nothing()
    {
        var presets = new StubPresetRepository();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            new CreateSearchPresetHandler(presets, Actor.Manager).Handle(
                new CreateSearchPresetCommand(
                    "Mala",
                    new SearchFiltersInput(null, ["archived"], null, null, null, null, null, null, null)),
                CancellationToken.None));

        Assert.Empty(presets.All);
    }

    [Theory]
    [InlineData("Inglés B2", "ingles b2")]
    [InlineData("Java Senior", "JAVA SENIOR")]
    [InlineData("Búsqueda", "Busqueda")]
    public async Task A_name_differing_only_by_case_or_accents_is_a_conflict(string existing, string attempted)
    {
        var presets = new StubPresetRepository();
        await new CreateSearchPresetHandler(presets, Actor.Manager)
            .Handle(new CreateSearchPresetCommand(existing, null), CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateSearchPresetHandler(presets, Actor.Manager)
                .Handle(new CreateSearchPresetCommand(attempted, null), CancellationToken.None));

        Assert.Equal(SearchErrors.PresetNameConflict, conflict.Code);
        Assert.Equal(existing, presets.Single().Name);
    }

    [Fact]
    public void Preset_names_normalize_like_catalog_names()
    {
        Assert.Equal("ingles b2", SearchPresetName.Normalize("  Inglés B2 "));
        Assert.Equal(string.Empty, SearchPresetName.Normalize(null));
    }

    [Fact]
    public async Task Updating_preserves_identity_and_creation_time_and_advances_the_update_time_and_version()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Manager)
            .Handle(new CreateSearchPresetCommand("Original", null), CancellationToken.None);
        await Task.Delay(5);

        var updated = await new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
            new UpdateSearchPresetCommand(
                created.Id,
                "Renombrada",
                new SearchFiltersInput(null, null, null, null, null, null, null, null, "yes"),
                created.Version),
            CancellationToken.None);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
        Assert.Equal("Renombrada", updated.Name);
        Assert.Equal("yes", updated.Filters.HasCv);
        Assert.Equal(created.Version + 1, updated.Version);
    }

    [Fact]
    public async Task A_stale_version_is_refused_on_update()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Manager)
            .Handle(new CreateSearchPresetCommand("Original", null), CancellationToken.None);
        await new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
            new UpdateSearchPresetCommand(created.Id, "Primera edición", null, created.Version),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
                new UpdateSearchPresetCommand(created.Id, "Segunda edición", null, created.Version),
                CancellationToken.None));

        Assert.Equal(SearchErrors.PresetConcurrencyConflict, conflict.Code);
        Assert.NotEqual(SearchErrors.PresetNameConflict, conflict.Code);
    }

    [Fact]
    public async Task A_stale_version_is_refused_on_delete_and_the_preset_remains()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Manager)
            .Handle(new CreateSearchPresetCommand("Original", null), CancellationToken.None);
        await new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
            new UpdateSearchPresetCommand(created.Id, "Editada", null, created.Version),
            CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new DeleteSearchPresetHandler(presets, Actor.Manager)
                .Handle(new DeleteSearchPresetCommand(created.Id, created.Version), CancellationToken.None));

        Assert.Equal(SearchErrors.PresetConcurrencyConflict, conflict.Code);
        Assert.Equal(created.Id, presets.Single().Id);
    }

    [Fact]
    public void A_version_that_was_never_issued_is_refused_by_validation()
    {
        var update = new UpdateSearchPresetValidator().Validate(
            new UpdateSearchPresetCommand(Guid.NewGuid(), "Nombre", null, 0));
        var delete = new DeleteSearchPresetValidator().Validate(new DeleteSearchPresetCommand(Guid.NewGuid(), 0));

        Assert.Contains(update.Errors, error => error.ErrorCode == SearchErrors.PresetVersionInvalid);
        Assert.Contains(delete.Errors, error => error.ErrorCode == SearchErrors.PresetVersionInvalid);
    }

    [Fact]
    public async Task Applying_a_preset_returns_its_filters_and_advances_only_the_last_used_time()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Manager).Handle(
            new CreateSearchPresetCommand(
                "Con CV",
                new SearchFiltersInput(null, null, null, null, null, null, null, null, "yes")),
            CancellationToken.None);
        await Task.Delay(5);

        var applied = await new UseSearchPresetHandler(presets, Actor.Reader)
            .Handle(new UseSearchPresetCommand(created.Id), CancellationToken.None);

        Assert.Equal("yes", applied.Filters.HasCv);
        Assert.NotNull(applied.LastUsedAt);
        Assert.True(applied.LastUsedAt > created.UpdatedAt);
        Assert.Equal(created.UpdatedAt, applied.UpdatedAt);
        Assert.Equal(created.Version, applied.Version);
    }

    [Fact]
    public async Task Applying_a_preset_does_not_make_a_pending_edit_stale()
    {
        var presets = new StubPresetRepository();
        var loadedByAdministrator = await new CreateSearchPresetHandler(presets, Actor.Manager)
            .Handle(new CreateSearchPresetCommand("Compartida", null), CancellationToken.None);

        await new UseSearchPresetHandler(presets, Actor.Reader)
            .Handle(new UseSearchPresetCommand(loadedByAdministrator.Id), CancellationToken.None);
        var saved = await new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
            new UpdateSearchPresetCommand(loadedByAdministrator.Id, "Compartida v2", null, loadedByAdministrator.Version),
            CancellationToken.None);

        Assert.Equal("Compartida v2", saved.Name);
    }

    [Fact]
    public async Task Deleting_removes_only_that_preset()
    {
        var presets = new StubPresetRepository();
        var other = presets.Seed("Otra");
        var target = presets.Seed("Objetivo");

        await new DeleteSearchPresetHandler(presets, Actor.Manager)
            .Handle(new DeleteSearchPresetCommand(target.Id, 1), CancellationToken.None);

        Assert.Equal(other.Id, Assert.Single(presets.All).Id);
    }

    [Fact]
    public async Task A_missing_preset_is_the_same_not_found_for_every_operation()
    {
        var presets = new StubPresetRepository();
        var missing = Guid.NewGuid();

        var refusals = new[]
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new GetSearchPresetHandler(presets, Actor.Reader)
                    .Handle(new GetSearchPresetQuery(missing), CancellationToken.None)),
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new UseSearchPresetHandler(presets, Actor.Reader)
                    .Handle(new UseSearchPresetCommand(missing), CancellationToken.None)),
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new UpdateSearchPresetHandler(presets, Actor.Manager)
                    .Handle(new UpdateSearchPresetCommand(missing, "Nombre", null, 1), CancellationToken.None)),
            await Assert.ThrowsAsync<NotFoundException>(() =>
                new DeleteSearchPresetHandler(presets, Actor.Manager)
                    .Handle(new DeleteSearchPresetCommand(missing, 1), CancellationToken.None)),
        };

        Assert.All(refusals, refusal => Assert.Equal(SearchErrors.PresetNotFound, refusal.Code));
        Assert.Equal(0, presets.SaveCount);
    }

    [Fact]
    public async Task A_refusal_never_repeats_the_name_or_the_filters_it_was_given()
    {
        var presets = new StubPresetRepository();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateSearchPresetHandler(presets, Actor.Manager).Handle(
                new UpdateSearchPresetCommand(
                    Guid.NewGuid(),
                    "Búsqueda de Marta",
                    new SearchFiltersInput("Marta Ruiz", null, null, null, null, null, null, null, null),
                    1),
                CancellationToken.None));

        Assert.DoesNotContain("Marta", refusal.Message, StringComparison.Ordinal);
    }

    private static async Task AssertEveryPresetHandlerRefuses(
        StubPresetRepository presets,
        Guid existing,
        Actor actor,
        bool reads,
        bool writes)
    {
        if (reads)
        {
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new ListSearchPresetsHandler(presets, actor).Handle(new ListSearchPresetsQuery(), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new GetSearchPresetHandler(presets, actor).Handle(new GetSearchPresetQuery(existing), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new UseSearchPresetHandler(presets, actor).Handle(new UseSearchPresetCommand(existing), CancellationToken.None));
        }
        if (writes)
        {
            // Deliberately invalid input: authorization must come before validation, so the
            // refusal is Forbidden rather than a validation problem.
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new CreateSearchPresetHandler(presets, actor).Handle(
                    new CreateSearchPresetCommand("", null), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new UpdateSearchPresetHandler(presets, actor).Handle(
                    new UpdateSearchPresetCommand(existing, "", null, 1), CancellationToken.None));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                new DeleteSearchPresetHandler(presets, actor).Handle(
                    new DeleteSearchPresetCommand(existing, 1), CancellationToken.None));
        }
    }

    private sealed class RecordingCandidateRepository : ICandidateRepository
    {
        public SearchFiltersValue? LastFilters { get; private set; }
        public (int Page, int PageSize)? LastPaging { get; private set; }
        public SearchOptions? LastOptions { get; private set; }

        public Task<SearchPage<CandidateSearchItem>> SearchAsync(
            SearchFiltersValue filters,
            SearchOptions options,
            CancellationToken cancellationToken)
        {
            LastFilters = filters;
            LastPaging = (options.Page, options.PageSize);
            LastOptions = options;
            return Task.FromResult(new SearchPage<CandidateSearchItem>([], options.Page, options.PageSize, 0));
        }

        public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Candidate?>(null);

        public Task<Candidate?> FindCoreAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Candidate?>(null);

        public Task<IReadOnlyList<CandidateSummary>> ListAsync(
            bool includeInactive,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateSummary>>([]);

        public Task<IReadOnlyList<CandidateDocument>> ListDocumentsAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateDocument>>([]);

        public Task<IReadOnlyList<CandidateNote>> ListNotesAsync(
            Guid candidateId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidateNote>>([]);

        public Task<CandidateNote?> FindNoteAsync(
            Guid candidateId,
            Guid noteId,
            bool includeInactive,
            CancellationToken cancellationToken) =>
            Task.FromResult<CandidateNote?>(null);

        public void Add(Candidate candidate) { }

        public void AddRelation(CandidateRelation relation) { }

        public void AddNote(CandidateNote note) { }

        public void RemoveRelations(IEnumerable<CandidateRelation> relations) { }

        public void ExpectVersion(Candidate candidate, uint version) { }

        public void ExpectVersion(CandidateNote note, uint version) { }

        public Task<CandidateSaveOutcome> SaveAsync(
            string auditEventType,
            string subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CandidateSaveOutcome.Saved);

        public Task<CandidateSaveOutcome> SaveNoteAsync(
            string auditEventType,
            Guid candidateId,
            Guid noteId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CandidateSaveOutcome.Saved);
    }

    /// <summary>
    /// An in-memory stand-in that keeps the properties the real store guarantees: names are
    /// unique across the library under folded comparison, and a write against a version other
    /// than the one the preset held when it was expected is refused.
    /// </summary>
    private sealed class StubPresetRepository : ISearchPresetRepository
    {
        private readonly List<SearchPreset> _stored = [];
        private readonly List<SearchPreset> _pending = [];
        private readonly List<SearchPreset> _removed = [];
        private bool _staleVersionExpected;

        public IReadOnlyList<SearchPreset> All => _stored;

        public int SaveCount { get; private set; }

        public SearchPreset Single() => Assert.Single(_stored);

        public SearchPreset Seed(string name)
        {
            var preset = new SearchPreset(
                Guid.CreateVersion7(),
                name,
                SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters")),
                SearchFilterNormalization.FilterSchemaVersion,
                DateTimeOffset.UtcNow);
            _stored.Add(preset);
            return preset;
        }

        public Task<IReadOnlyList<SearchPreset>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchPreset>>(
                [.. _stored.OrderBy(preset => preset.NormalizedName, StringComparer.Ordinal)]);

        public Task<SearchPreset?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_stored.SingleOrDefault(preset => preset.Id == id));

        public void Add(SearchPreset preset) => _pending.Add(preset);

        public void Remove(SearchPreset preset) => _removed.Add(preset);

        // Checked at the moment of expectation, before the handler mutates the preset, which is
        // what the database's WHERE "Version" = @original does at save time.
        public void ExpectVersion(SearchPreset preset, uint version) =>
            _staleVersionExpected |= preset.Version != (int)version;

        public Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            if (_staleVersionExpected)
            {
                Reset();
                return Task.FromResult(SearchPresetSaveOutcome.ConcurrencyConflict);
            }
            if (_pending.Concat(_stored)
                .Except(_removed)
                .GroupBy(preset => preset.NormalizedName)
                .Any(group => group.Count() > 1))
            {
                Reset();
                return Task.FromResult(SearchPresetSaveOutcome.NameConflict);
            }
            _stored.AddRange(_pending);
            foreach (var preset in _removed)
            {
                _stored.Remove(preset);
            }
            Reset();
            return Task.FromResult(SearchPresetSaveOutcome.Saved);
        }

        private void Reset()
        {
            _pending.Clear();
            _removed.Clear();
            _staleVersionExpected = false;
        }
    }

    private sealed class Actor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static Actor Anonymous => new(false);
        public static Actor WithoutPermissions => new(true);
        public static Actor Reader => new(true, Permissions.CandidatesRead);
        public static Actor Remover => new(true, Permissions.CandidatesRead, Permissions.CandidatesDelete);
        public static Actor Manager => new(true, Permissions.CandidatesRead, Permissions.PresetsManage);
        public static Actor ManagerOnly => new(true, Permissions.PresetsManage);

        /// <summary>
        /// A hypothetical broader read permission has no backend capability of its own and no
        /// scoping effect; this actor exists to prove that. KTL-16 removed the frontend's inert
        /// <c>view_all_candidates</c> for the same reason.
        /// </summary>
        public static Actor ReaderWithViewAll => new(true, Permissions.CandidatesRead, "candidates.read_all");

        public string? ExternalKey => authenticated ? "test-actor" : null;

        /// <summary>No stored user stands behind a test double; nothing under test reads it.</summary>
        public Guid? UserId => null;

        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
