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
/// before and around the query — authorization, normalization, page bounds, owner scoping
/// and refusals. The query's own semantics are SQL, and are proven against PostgreSQL.
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
    public async Task Actors_differing_only_by_view_all_candidates_issue_the_same_query()
    {
        // KTL-10 defines no visibility scope, so there is nothing for the broader permission
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
    public async Task Presets_are_listed_only_for_the_current_actor()
    {
        var presets = new StubPresetRepository();
        presets.Seed("someone-else", "Ajena");
        var mine = presets.Seed("test-actor", "Propia");

        var listed = await new ListSearchPresetsHandler(presets, Actor.Reader)
            .Handle(new ListSearchPresetsQuery(), CancellationToken.None);

        Assert.Equal(mine.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task An_actor_without_a_stable_key_cannot_own_presets()
    {
        // Defaulting to the empty string would silently pool every such actor's saved
        // searches into one shared owner.
        var presets = new StubPresetRepository();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListSearchPresetsHandler(presets, Actor.KeylessReader)
                .Handle(new ListSearchPresetsQuery(), CancellationToken.None));
    }

    [Fact]
    public async Task Creating_a_preset_derives_its_owner_and_stamps_server_timestamps()
    {
        var presets = new StubPresetRepository();
        var before = DateTimeOffset.UtcNow;

        var created = await new CreateSearchPresetHandler(presets, Actor.Reader).Handle(
            new CreateSearchPresetCommand("  Java senior  ", null),
            CancellationToken.None);

        var stored = presets.Single();
        Assert.Equal("test-actor", stored.OwnerId);
        Assert.Equal("Java senior", created.Name);
        Assert.True(created.CreatedAt >= before);
        Assert.Equal(created.CreatedAt, created.UpdatedAt);
        Assert.Null(created.LastUsedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_preset_name_is_refused(string name)
    {
        var failure = await Assert.ThrowsAsync<RequestValidationException>(() =>
            new CreateSearchPresetHandler(new StubPresetRepository(), Actor.Reader)
                .Handle(new CreateSearchPresetCommand(name, null), CancellationToken.None));

        Assert.Contains(failure.Issues, issue => issue.Code == SearchErrors.PresetNameRequired);
    }

    [Fact]
    public async Task An_invalid_filter_is_refused_and_stores_nothing()
    {
        var presets = new StubPresetRepository();

        await Assert.ThrowsAsync<RequestValidationException>(() =>
            new CreateSearchPresetHandler(presets, Actor.Reader).Handle(
                new CreateSearchPresetCommand(
                    "Mala",
                    new SearchFiltersInput(null, ["archived"], null, null, null, null, null, null, null)),
                CancellationToken.None));

        Assert.Empty(presets.All);
    }

    [Fact]
    public async Task A_name_differing_only_by_case_is_a_conflict()
    {
        var presets = new StubPresetRepository();
        await new CreateSearchPresetHandler(presets, Actor.Reader)
            .Handle(new CreateSearchPresetCommand("Java Senior", null), CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<ConflictException>(() =>
            new CreateSearchPresetHandler(presets, Actor.Reader)
                .Handle(new CreateSearchPresetCommand("java senior", null), CancellationToken.None));

        Assert.Equal(SearchErrors.PresetNameConflict, conflict.Code);
        Assert.Single(presets.All);
    }

    [Fact]
    public async Task Updating_preserves_identity_and_creation_time_and_advances_the_update_time()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Reader)
            .Handle(new CreateSearchPresetCommand("Original", null), CancellationToken.None);
        await Task.Delay(5);

        var updated = await new UpdateSearchPresetHandler(presets, Actor.Reader).Handle(
            new UpdateSearchPresetCommand(
                created.Id,
                "Renombrada",
                new SearchFiltersInput(null, null, null, null, null, null, null, null, "yes")),
            CancellationToken.None);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
        Assert.Equal("Renombrada", updated.Name);
        Assert.Equal("yes", updated.Filters.HasCv);
        Assert.Equal("test-actor", presets.Single().OwnerId);
    }

    [Fact]
    public async Task Applying_a_preset_returns_its_filters_and_advances_both_timestamps()
    {
        var presets = new StubPresetRepository();
        var created = await new CreateSearchPresetHandler(presets, Actor.Reader).Handle(
            new CreateSearchPresetCommand(
                "Con CV",
                new SearchFiltersInput(null, null, null, null, null, null, null, null, "yes")),
            CancellationToken.None);
        await Task.Delay(5);

        var applied = await new UseSearchPresetHandler(presets, Actor.Reader)
            .Handle(new UseSearchPresetCommand(created.Id), CancellationToken.None);

        Assert.Equal("yes", applied.Filters.HasCv);
        Assert.NotNull(applied.LastUsedAt);
        Assert.Equal(applied.LastUsedAt, applied.UpdatedAt);
        Assert.True(applied.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Deleting_removes_only_that_owner_preset()
    {
        var presets = new StubPresetRepository();
        var other = presets.Seed("someone-else", "Ajena");
        var mine = await new CreateSearchPresetHandler(presets, Actor.Reader)
            .Handle(new CreateSearchPresetCommand("Propia", null), CancellationToken.None);

        await new DeleteSearchPresetHandler(presets, Actor.Reader)
            .Handle(new DeleteSearchPresetCommand(mine.Id), CancellationToken.None);

        Assert.Equal(other.Id, Assert.Single(presets.All).Id);
    }

    [Fact]
    public async Task A_cross_owner_preset_is_indistinguishable_from_a_missing_one()
    {
        var presets = new StubPresetRepository();
        var theirs = presets.Seed("someone-else", "Ajena");

        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            new UseSearchPresetHandler(presets, Actor.Reader)
                .Handle(new UseSearchPresetCommand(Guid.NewGuid()), CancellationToken.None));
        var crossOwner = await Assert.ThrowsAsync<NotFoundException>(() =>
            new UseSearchPresetHandler(presets, Actor.Reader)
                .Handle(new UseSearchPresetCommand(theirs.Id), CancellationToken.None));

        Assert.Equal(missing.Code, crossOwner.Code);
        Assert.Equal(missing.Message, crossOwner.Message);
    }

    [Fact]
    public async Task A_refusal_never_names_another_owner_or_repeats_the_filters()
    {
        var presets = new StubPresetRepository();
        var theirs = presets.Seed("someone-else", "Búsqueda de Marta");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            new UpdateSearchPresetHandler(presets, Actor.Reader)
                .Handle(new UpdateSearchPresetCommand(theirs.Id, "Mía", null), CancellationToken.None));

        Assert.DoesNotContain("someone-else", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Marta", refusal.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingCandidateRepository : ICandidateRepository
    {
        public SearchFiltersValue? LastFilters { get; private set; }
        public (int Page, int PageSize)? LastPaging { get; private set; }

        public Task<SearchPage<CandidateSearchItem>> SearchAsync(
            SearchFiltersValue filters,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            LastFilters = filters;
            LastPaging = (page, pageSize);
            return Task.FromResult(new SearchPage<CandidateSearchItem>([], page, pageSize, 0));
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

        public void Add(Candidate candidate) { }

        public void AddRelation(CandidateRelation relation) { }

        public void RemoveRelations(IEnumerable<CandidateRelation> relations) { }

        public void ExpectVersion(Candidate candidate, uint version) { }

        public Task<CandidateSaveOutcome> SaveAsync(
            string auditEventType,
            string subjectId,
            CancellationToken cancellationToken) =>
            Task.FromResult(CandidateSaveOutcome.Saved);
    }

    /// <summary>
    /// An in-memory stand-in that keeps the two properties the real store guarantees: every
    /// lookup is owner-scoped, and the per-owner name is unique without regard to case.
    /// </summary>
    private sealed class StubPresetRepository : ISearchPresetRepository
    {
        private readonly List<SearchPreset> _stored = [];
        private readonly List<SearchPreset> _pending = [];
        private readonly List<SearchPreset> _removed = [];

        public IReadOnlyList<SearchPreset> All => _stored;

        public SearchPreset Single() => Assert.Single(_stored);

        public SearchPreset Seed(string owner, string name)
        {
            var preset = new SearchPreset(
                Guid.CreateVersion7(),
                owner,
                name,
                SearchFilterDocument.Serialize(SearchFilterNormalization.Normalize(null, "Filters")),
                SearchFilterNormalization.FilterSchemaVersion,
                DateTimeOffset.UtcNow);
            _stored.Add(preset);
            return preset;
        }

        public Task<IReadOnlyList<SearchPreset>> ListAsync(string ownerId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SearchPreset>>(
            [
                .. _stored
                    .Where(preset => preset.OwnerId == ownerId)
                    .OrderBy(preset => preset.NormalizedName, StringComparer.Ordinal),
            ]);

        public Task<SearchPreset?> FindAsync(string ownerId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(_stored.SingleOrDefault(preset => preset.Id == id && preset.OwnerId == ownerId));

        public void Add(SearchPreset preset) => _pending.Add(preset);

        public void Remove(SearchPreset preset) => _removed.Add(preset);

        public Task<SearchPresetSaveOutcome> SaveAsync(CancellationToken cancellationToken)
        {
            var candidates = _pending.Concat(_stored).ToList();
            if (candidates
                .GroupBy(preset => (preset.OwnerId, preset.NormalizedName))
                .Any(group => group.Count() > 1))
            {
                _pending.Clear();
                _removed.Clear();
                return Task.FromResult(SearchPresetSaveOutcome.NameConflict);
            }
            _stored.AddRange(_pending);
            foreach (var preset in _removed)
            {
                _stored.Remove(preset);
            }
            _pending.Clear();
            _removed.Clear();
            return Task.FromResult(SearchPresetSaveOutcome.Saved);
        }
    }

    private sealed class Actor(bool authenticated, string? key, params string[] permissions) : ICurrentActor
    {
        public static Actor Anonymous => new(false, null);
        public static Actor WithoutPermissions => new(true, "test-actor");
        public static Actor Reader => new(true, "test-actor", Permissions.CandidatesRead);

        /// <summary>
        /// The frontend's <c>view_all_candidates</c> has no backend capability of its own and
        /// no scoping effect; this actor exists to prove that.
        /// </summary>
        public static Actor ReaderWithViewAll =>
            new(true, "test-actor", Permissions.CandidatesRead, "candidates.read_all");

        public static Actor KeylessReader => new(true, "   ", Permissions.CandidatesRead);

        public string? ExternalKey => key;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
