using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Application.Features.Positions;
using KeplerTalento.Domain.Auditing;
using KeplerTalento.Domain.Positions;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

public sealed class PositionCandidateHandlerTests
{
    // ---- domain ----

    [Fact]
    public void Stages_are_the_closed_vocabulary_in_display_order()
    {
        Assert.Equal(["new", "shortlisted", "interview", "hired", "rejected"], PositionCandidateStages.All);
        Assert.False(PositionCandidateStages.IsKnown("NEW"));
        Assert.False(PositionCandidateStages.IsKnown("referred"));
        Assert.False(PositionCandidateStages.IsKnown(null));
    }

    [Fact]
    public void A_new_link_starts_at_new_and_any_stage_can_follow_any_other()
    {
        var added = DateTimeOffset.UtcNow;
        var link = new PositionCandidate(Guid.CreateVersion7(), Guid.NewGuid(), Guid.NewGuid(), added);
        Assert.Equal(PositionCandidateStages.New, link.Stage);

        link.ChangeStage(PositionCandidateStages.Rejected, added.AddMinutes(1));
        link.ChangeStage(PositionCandidateStages.Interview, added.AddMinutes(2));

        Assert.Equal(PositionCandidateStages.Interview, link.Stage);
        Assert.Equal(added.AddMinutes(2), link.UpdatedAtUtc);
        Assert.Throws<ArgumentOutOfRangeException>(() => link.ChangeStage("archived", added));
    }

    [Fact]
    public void The_audit_subject_names_both_ids_and_nothing_else()
    {
        var position = Guid.Parse("0192a0b1-0000-7000-8000-000000000001");
        var candidate = Guid.Parse("0192a0b1-0000-7000-8000-000000000002");

        var subject = PositionCandidate.AuditSubject(position, candidate);

        Assert.Equal("0192a0b100007000800000000000000" + "1:0192a0b100007000800000000000000" + "2", subject);
        Assert.True(subject.Length <= 100);
    }

    [Fact]
    public void The_audit_catalogue_contains_the_link_events()
    {
        Assert.Contains(PositionAuditEvents.CandidateAdded, AuditEventTypes.All);
        Assert.Contains(PositionAuditEvents.CandidateStageChanged, AuditEventTypes.All);
        Assert.Contains(PositionAuditEvents.CandidateRemoved, AuditEventTypes.All);
    }

    // ---- guards ----

    public static TheoryData<string> ReadRefusals => new() { "unauthenticated", "positions-only", "candidates-only", "manage-only" };
    public static TheoryData<string> WriteRefusals => new() { "unauthenticated", "reader", "manage-without-candidates", "candidates-only" };

    [Theory]
    [MemberData(nameof(ReadRefusals))]
    public async Task Reads_refuse_without_both_permissions_before_any_lookup(string actorName)
    {
        var repository = new StubRepository();
        var actor = Actor.Named(actorName);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListPositionCandidatesHandler(repository, actor).Handle(new(repository.OpenPosition), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListPositionCandidatesHandler(repository, actor).Handle(new(Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListCandidatePositionsHandler(repository, actor).Handle(new(repository.ActiveCandidate), CancellationToken.None));

        Assert.Equal(0, repository.Calls);
    }

    [Theory]
    [MemberData(nameof(WriteRefusals))]
    public async Task Writes_refuse_without_both_permissions_before_validating_or_looking_up(string actorName)
    {
        var repository = new StubRepository();
        var actor = Actor.Named(actorName);

        // Invalid on every member: a validation problem here would disclose the contract.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new AddPositionCandidateHandler(repository, actor).Handle(new(Guid.NewGuid(), null), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ChangePositionCandidateStageHandler(repository, actor).Handle(new(Guid.NewGuid(), Guid.NewGuid(), "nope", 0), CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            new RemovePositionCandidateHandler(repository, actor).Handle(new(repository.OpenPosition, repository.ActiveCandidate), CancellationToken.None));

        Assert.Equal(0, repository.Calls);
        Assert.Empty(repository.AuditEvents);
    }

    // ---- add ----

    [Fact]
    public async Task Add_links_a_candidate_at_new_without_evaluating_requirements()
    {
        var repository = new StubRepository();

        var added = await new AddPositionCandidateHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.ActiveCandidate), CancellationToken.None);

        Assert.Equal(repository.ActiveCandidate, added.CandidateId);
        Assert.Equal(PositionCandidateStages.New, added.Stage);
        var link = Assert.Single(repository.Links);
        Assert.Equal(7, link.Id.Version);
        Assert.Equal((PositionAuditEvents.CandidateAdded, PositionCandidate.AuditSubject(repository.OpenPosition, repository.ActiveCandidate)), Assert.Single(repository.AuditEvents));
    }

    [Fact]
    public async Task Add_refuses_a_closed_position()
    {
        var repository = new StubRepository();
        var exception = await Assert.ThrowsAsync<ConflictException>(() => new AddPositionCandidateHandler(repository, Actor.Manager)
            .Handle(new(repository.ClosedPosition, repository.ActiveCandidate), CancellationToken.None));
        Assert.Equal(PositionErrors.CandidatePositionClosed, exception.Code);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task Add_refuses_a_removed_candidate()
    {
        var repository = new StubRepository();
        var exception = await Assert.ThrowsAsync<ConflictException>(() => new AddPositionCandidateHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.RemovedCandidate), CancellationToken.None));
        Assert.Equal(PositionErrors.CandidateInactive, exception.Code);
        Assert.Empty(repository.Links);
    }

    [Fact]
    public async Task Add_reports_unknown_position_and_candidate_as_not_found()
    {
        var repository = new StubRepository();
        var handler = new AddPositionCandidateHandler(repository, Actor.Manager);

        var position = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(Guid.NewGuid(), repository.ActiveCandidate), CancellationToken.None));
        var candidate = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(repository.OpenPosition, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(PositionErrors.NotFound, position.Code);
        Assert.Equal(CandidateErrors.NotFound, candidate.Code);
    }

    [Fact]
    public async Task Add_refuses_a_candidate_already_linked_and_a_lost_race()
    {
        var repository = new StubRepository();
        var handler = new AddPositionCandidateHandler(repository, Actor.Manager);
        await handler.Handle(new(repository.OpenPosition, repository.ActiveCandidate), CancellationToken.None);

        var again = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(repository.OpenPosition, repository.ActiveCandidate), CancellationToken.None));
        Assert.Equal(PositionErrors.CandidateAlreadyLinked, again.Code);

        var racing = new StubRepository { NextOutcome = PositionSaveOutcome.AlreadyLinked };
        var raced = await Assert.ThrowsAsync<ConflictException>(() => new AddPositionCandidateHandler(racing, Actor.Manager)
            .Handle(new(racing.OpenPosition, racing.ActiveCandidate), CancellationToken.None));
        Assert.Equal(PositionErrors.CandidateAlreadyLinked, raced.Code);
    }

    [Fact]
    public async Task Add_refuses_a_position_at_the_link_limit()
    {
        var repository = new StubRepository { LinkCountOverride = PositionCandidate.MaximumPerPosition };
        var exception = await Assert.ThrowsAsync<ConflictException>(() => new AddPositionCandidateHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.ActiveCandidate), CancellationToken.None));
        Assert.Equal(PositionErrors.CandidateLimitReached, exception.Code);
    }

    [Fact]
    public void Add_validator_requires_a_candidate_id()
    {
        var validator = new AddPositionCandidateValidator();
        Assert.Equal(PositionErrors.CandidateRequired, Assert.Single(validator.Validate(new AddPositionCandidateCommand(Guid.NewGuid(), null)).Errors).ErrorCode);
        Assert.Equal(PositionErrors.CandidateRequired, Assert.Single(validator.Validate(new AddPositionCandidateCommand(Guid.NewGuid(), Guid.Empty)).Errors).ErrorCode);
    }

    // ---- stage ----

    [Fact]
    public async Task Stage_change_forwards_the_version_and_audits_without_the_stage()
    {
        var repository = new StubRepository();
        repository.Seed(repository.OpenPosition, repository.ActiveCandidate);

        var changed = await new ChangePositionCandidateStageHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.ActiveCandidate, PositionCandidateStages.Interview, 4), CancellationToken.None);

        Assert.Equal(PositionCandidateStages.Interview, changed.Stage);
        Assert.Equal(4u, repository.ExpectedVersion);
        var (type, subject) = Assert.Single(repository.AuditEvents);
        Assert.Equal(PositionAuditEvents.CandidateStageChanged, type);
        Assert.DoesNotContain("interview", subject, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Stage_change_reports_stale_versions_closed_positions_missing_links_and_unknown_stages()
    {
        var stale = new StubRepository { NextOutcome = PositionSaveOutcome.ConcurrencyConflict };
        stale.Seed(stale.OpenPosition, stale.ActiveCandidate);
        Assert.Equal(PositionErrors.CandidateConcurrencyConflict, (await Assert.ThrowsAsync<ConflictException>(() =>
            new ChangePositionCandidateStageHandler(stale, Actor.Manager).Handle(new(stale.OpenPosition, stale.ActiveCandidate, PositionCandidateStages.Hired, 1), CancellationToken.None))).Code);

        var closed = new StubRepository();
        closed.Seed(closed.ClosedPosition, closed.ActiveCandidate);
        Assert.Equal(PositionErrors.CandidatePositionClosed, (await Assert.ThrowsAsync<ConflictException>(() =>
            new ChangePositionCandidateStageHandler(closed, Actor.Manager).Handle(new(closed.ClosedPosition, closed.ActiveCandidate, PositionCandidateStages.Hired, 1), CancellationToken.None))).Code);

        var missing = new StubRepository();
        Assert.Equal(PositionErrors.CandidateLinkNotFound, (await Assert.ThrowsAsync<NotFoundException>(() =>
            new ChangePositionCandidateStageHandler(missing, Actor.Manager).Handle(new(missing.OpenPosition, missing.ActiveCandidate, PositionCandidateStages.Hired, 1), CancellationToken.None))).Code);

        Assert.Equal(PositionErrors.CandidateStageInvalid, Assert.Single((await Assert.ThrowsAsync<RequestValidationException>(() =>
            new ChangePositionCandidateStageHandler(missing, Actor.Manager).Handle(new(missing.OpenPosition, missing.ActiveCandidate, "referred", 1), CancellationToken.None))).Issues).Code);
    }

    [Fact]
    public async Task Stage_of_a_removed_candidate_can_still_change()
    {
        var repository = new StubRepository();
        repository.Seed(repository.OpenPosition, repository.RemovedCandidate);

        var changed = await new ChangePositionCandidateStageHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.RemovedCandidate, PositionCandidateStages.Rejected, 1), CancellationToken.None);

        Assert.Equal(PositionCandidateStages.Rejected, changed.Stage);
        Assert.False(changed.CandidateIsActive);
    }

    // ---- remove ----

    [Fact]
    public async Task Remove_deletes_the_link_and_audits_both_ids()
    {
        var repository = new StubRepository();
        repository.Seed(repository.OpenPosition, repository.RemovedCandidate);

        await new RemovePositionCandidateHandler(repository, Actor.Manager)
            .Handle(new(repository.OpenPosition, repository.RemovedCandidate), CancellationToken.None);

        Assert.Empty(repository.Links);
        Assert.Equal((PositionAuditEvents.CandidateRemoved, PositionCandidate.AuditSubject(repository.OpenPosition, repository.RemovedCandidate)), Assert.Single(repository.AuditEvents));
    }

    [Fact]
    public async Task Remove_refuses_closed_positions_and_missing_links()
    {
        var closed = new StubRepository();
        closed.Seed(closed.ClosedPosition, closed.ActiveCandidate);
        Assert.Equal(PositionErrors.CandidatePositionClosed, (await Assert.ThrowsAsync<ConflictException>(() =>
            new RemovePositionCandidateHandler(closed, Actor.Manager).Handle(new(closed.ClosedPosition, closed.ActiveCandidate), CancellationToken.None))).Code);
        Assert.Single(closed.Links);

        var missing = new StubRepository();
        Assert.Equal(PositionErrors.CandidateLinkNotFound, (await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemovePositionCandidateHandler(missing, Actor.Manager).Handle(new(missing.OpenPosition, missing.ActiveCandidate), CancellationToken.None))).Code);
    }

    // ---- lists ----

    [Fact]
    public async Task Lists_report_unknown_positions_and_candidates_as_not_found()
    {
        var repository = new StubRepository();
        Assert.Equal(PositionErrors.NotFound, (await Assert.ThrowsAsync<NotFoundException>(() =>
            new ListPositionCandidatesHandler(repository, Actor.Reader).Handle(new(Guid.NewGuid()), CancellationToken.None))).Code);
        Assert.Equal(CandidateErrors.NotFound, (await Assert.ThrowsAsync<NotFoundException>(() =>
            new ListCandidatePositionsHandler(repository, Actor.Reader).Handle(new(Guid.NewGuid()), CancellationToken.None))).Code);
    }

    [Fact]
    public void List_projections_carry_only_search_contact_columns_and_no_description_or_requirements()
    {
        Assert.Equal(
            ["AddedAtUtc", "CandidateId", "CandidateIsActive", "Email", "FirstName", "HasPrimaryCv", "LastName", "Phone", "Stage", "UpdatedAtUtc", "Version"],
            typeof(PositionCandidateResponse).GetProperties().Select(property => property.Name).Order());
        Assert.Equal(
            ["AddedAtUtc", "PositionId", "PositionStatus", "Stage", "Title", "UpdatedAtUtc", "Version"],
            typeof(CandidatePositionResponse).GetProperties().Select(property => property.Name).Order());
    }

    // ---- doubles ----

    private sealed class StubRepository : IPositionRepository
    {
        public Guid OpenPosition { get; } = Guid.CreateVersion7();
        public Guid ClosedPosition { get; } = Guid.CreateVersion7();
        public Guid ActiveCandidate { get; } = Guid.CreateVersion7();
        public Guid RemovedCandidate { get; } = Guid.CreateVersion7();
        public List<PositionCandidate> Links { get; } = [];
        public List<(string Type, string Subject)> AuditEvents { get; } = [];
        public int Calls { get; private set; }
        public uint? ExpectedVersion { get; private set; }
        public int? LinkCountOverride { get; init; }
        public PositionSaveOutcome NextOutcome { get; init; } = PositionSaveOutcome.Saved;
        private PositionCandidate? _pendingAdd;
        private PositionCandidate? _pendingRemove;

        public PositionCandidate Seed(Guid positionId, Guid candidateId)
        {
            var link = new PositionCandidate(Guid.CreateVersion7(), positionId, candidateId, DateTimeOffset.UtcNow);
            Links.Add(link);
            return link;
        }

        public Task<bool?> IsPositionOpenAsync(Guid positionId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<bool?>(positionId == OpenPosition ? true : positionId == ClosedPosition ? false : null);
        }

        public Task<bool?> IsCandidateActiveAsync(Guid candidateId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<bool?>(candidateId == ActiveCandidate ? true : candidateId == RemovedCandidate ? false : null);
        }

        public Task<PositionCandidate?> FindLinkAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Links.SingleOrDefault(link => link.PositionId == positionId && link.CandidateId == candidateId));
        }

        public Task<int> CountLinksAsync(Guid positionId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(LinkCountOverride ?? Links.Count(link => link.PositionId == positionId));
        }

        public void AddLink(PositionCandidate link) { Calls++; _pendingAdd = link; }
        public void RemoveLink(PositionCandidate link) { Calls++; _pendingRemove = link; }
        public void ExpectLinkVersion(PositionCandidate link, uint version) => ExpectedVersion = version;

        public Task<PositionSaveOutcome> SaveLinkAsync(string auditEventType, PositionCandidate link, CancellationToken cancellationToken)
        {
            Calls++;
            if (NextOutcome == PositionSaveOutcome.Saved)
            {
                if (_pendingAdd is not null) Links.Add(_pendingAdd);
                if (_pendingRemove is not null) Links.Remove(_pendingRemove);
                AuditEvents.Add((auditEventType, PositionCandidate.AuditSubject(link.PositionId, link.CandidateId)));
            }
            _pendingAdd = _pendingRemove = null;
            return Task.FromResult(NextOutcome);
        }

        public Task<PositionCandidateItem?> FindCandidateItemAsync(Guid positionId, Guid candidateId, CancellationToken cancellationToken) =>
            Task.FromResult(Links.Where(link => link.PositionId == positionId && link.CandidateId == candidateId).Select(ToItem).SingleOrDefault());

        public Task<IReadOnlyList<PositionCandidateItem>> ListCandidatesAsync(Guid positionId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PositionCandidateItem>>(Links.Where(link => link.PositionId == positionId).Select(ToItem).ToList());

        public Task<IReadOnlyList<CandidatePositionItem>> ListForCandidateAsync(Guid candidateId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CandidatePositionItem>>(Links.Where(link => link.CandidateId == candidateId)
                .Select(link => new CandidatePositionItem(link.PositionId, "Posición", link.PositionId == OpenPosition ? PositionStatuses.Open : PositionStatuses.Closed, link.Stage, link.AddedAtUtc, link.UpdatedAtUtc, link.Version))
                .ToList());

        private PositionCandidateItem ToItem(PositionCandidate link) =>
            new(link.CandidateId, "Ana", "García", "ana@example.test", "600000000", false, link.CandidateId != RemovedCandidate, link.Stage, link.AddedAtUtc, link.UpdatedAtUtc, link.Version);

        // Position members belong to PositionHandlerTests; these handlers never call them.
        public Task<PositionPage> ListAsync(PositionListOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Position?> FindAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Add(Position position) => throw new NotSupportedException();
        public void ExpectVersion(Position position, uint version) => throw new NotSupportedException();
        public Task<PositionSaveOutcome> SaveAsync(string auditEventType, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Actor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static Actor Manager => new(true, Permissions.PositionsRead, Permissions.PositionsManage, Permissions.CandidatesRead);
        public static Actor Reader => new(true, Permissions.PositionsRead, Permissions.CandidatesRead);

        public static Actor Named(string name) => name switch
        {
            "unauthenticated" => new(false, Permissions.PositionsRead, Permissions.PositionsManage, Permissions.CandidatesRead),
            "positions-only" => new(true, Permissions.PositionsRead, Permissions.PositionsManage),
            "candidates-only" => new(true, Permissions.CandidatesRead, Permissions.CandidatesUpdate),
            "manage-only" => new(true, Permissions.PositionsManage, Permissions.CandidatesRead),
            "reader" => Reader,
            "manage-without-candidates" => new(true, Permissions.PositionsRead, Permissions.PositionsManage),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

        public string? ExternalKey => authenticated ? "test-actor" : null;
        public Guid? UserId => null;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
