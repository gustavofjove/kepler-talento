using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Catalogs;
using KeplerTalento.Domain.Catalogs;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

public sealed class CatalogHandlerTests
{
    private const string Family = CatalogFamilies.Language;

    // ---- list ----

    [Fact]
    public async Task List_returns_the_family_in_position_order()
    {
        var repository = SeededRepository();
        var handler = new ListCatalogFamilyHandler(repository, Actor.Manager);

        var result = await handler.Handle(new(Family, false), CancellationToken.None);

        Assert.Equal(["Inglés", "Francés"], result.Select(item => item.NameEs));
    }

    [Fact]
    public async Task List_excludes_inactive_values_by_default()
    {
        var repository = SeededRepository();
        repository.Items[1].SetActive(false, DateTimeOffset.UtcNow);
        var handler = new ListCatalogFamilyHandler(repository, Actor.Manager);

        var active = await handler.Handle(new(Family, false), CancellationToken.None);
        var all = await handler.Handle(new(Family, true), CancellationToken.None);

        Assert.Equal(["Inglés"], active.Select(item => item.NameEs));
        Assert.Equal(2, all.Count);
        Assert.False(all.Single(item => item.NameEs == "Francés").IsActive);
    }

    [Fact]
    public async Task List_denies_an_actor_without_the_read_capability()
    {
        var handler = new ListCatalogFamilyHandler(SeededRepository(), Actor.None);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, false), CancellationToken.None));
    }

    [Fact]
    public async Task List_denies_an_unauthenticated_actor()
    {
        var handler = new ListCatalogFamilyHandler(SeededRepository(), Actor.Unauthenticated);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, false), CancellationToken.None));
    }

    [Fact]
    public async Task List_validator_rejects_an_unknown_family()
    {
        IValidator<ListCatalogFamilyQuery> validator = new ListCatalogFamilyValidator();
        var result = await validator.ValidateAsync(new("not_a_family", false));
        Assert.Equal(CatalogErrors.FamilyInvalid, Assert.Single(result.Errors).ErrorCode);
    }

    // ---- create ----

    [Fact]
    public async Task Create_appends_an_active_value_with_a_derived_code()
    {
        var repository = SeededRepository();
        var handler = new CreateCatalogItemHandler(repository, Actor.Manager);

        var created = await handler.Handle(new(Family, "  Alemán ", null, null), CancellationToken.None);

        Assert.Equal("Alemán", created.NameEs);
        Assert.Equal("ALEMAN", created.Code);
        Assert.Equal(3, created.SortOrder);
        Assert.True(created.IsActive);
        Assert.Equal(CatalogAuditEvents.Created, repository.LastAuditEventType);
    }

    [Fact]
    public async Task Create_uniquifies_a_code_that_is_already_taken()
    {
        var repository = SeededRepository();
        var handler = new CreateCatalogItemHandler(repository, Actor.Manager);

        var created = await handler.Handle(new(Family, "Otro", "INGLES", null), CancellationToken.None);

        Assert.Equal("INGLES_2", created.Code);
    }

    [Theory]
    [InlineData("Inglés")]
    [InlineData("ingles")]
    [InlineData("  INGLÉS  ")]
    public async Task Create_rejects_a_duplicate_name_with_the_Spanish_message(string name)
    {
        var handler = new CreateCatalogItemHandler(SeededRepository(), Actor.Manager);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, name, null, null), CancellationToken.None));

        var issue = Assert.Single(exception.Issues);
        Assert.Equal(CatalogErrors.NameDuplicate, issue.Code);
        Assert.Equal("Ya existe un valor con ese nombre.", issue.Message);
    }

    [Fact]
    public async Task Create_translates_a_unique_index_violation_to_the_duplicate_code()
    {
        var repository = SeededRepository();
        repository.NextOutcome = CatalogSaveOutcome.DuplicateName;
        var handler = new CreateCatalogItemHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, "Alemán", null, null), CancellationToken.None));

        Assert.Equal(CatalogErrors.NameDuplicate, Assert.Single(exception.Issues).Code);
    }

    [Fact]
    public async Task Create_denies_a_reader_and_stores_nothing()
    {
        var repository = SeededRepository();
        var handler = new CreateCatalogItemHandler(repository, Actor.Reader);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, "Alemán", null, null), CancellationToken.None));

        Assert.Equal(2, repository.Items.Count);
        Assert.Null(repository.LastAuditEventType);
    }

    [Fact]
    public async Task Create_denies_an_unauthenticated_actor()
    {
        var handler = new CreateCatalogItemHandler(SeededRepository(), Actor.Unauthenticated);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, "Alemán", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Create_validator_rejects_a_blank_name_with_the_Spanish_message()
    {
        IValidator<CreateCatalogItemCommand> validator = new CreateCatalogItemValidator();
        var result = await validator.ValidateAsync(new(Family, "   ", null, null));
        var error = Assert.Single(result.Errors);
        Assert.Equal(CatalogErrors.NameRequired, error.ErrorCode);
        Assert.Equal("El nombre es obligatorio.", error.ErrorMessage);
    }

    // ---- update ----

    [Fact]
    public async Task Update_renames_without_changing_position_or_activation()
    {
        var repository = SeededRepository();
        var target = repository.Items[1];
        var handler = new UpdateCatalogItemHandler(repository, Actor.Manager);

        var updated = await handler.Handle(
            new(Family, target.Id, "Francés antiguo", null, "Old French", 0),
            CancellationToken.None);

        Assert.Equal("Francés antiguo", updated.NameEs);
        Assert.Equal("Old French", updated.NameEn);
        Assert.Equal(2, updated.SortOrder);
        Assert.True(updated.IsActive);
        Assert.Equal(CatalogAuditEvents.Updated, repository.LastAuditEventType);
    }

    [Fact]
    public async Task Update_rejects_a_rename_that_collides_with_another_value()
    {
        var repository = SeededRepository();
        var handler = new UpdateCatalogItemHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, repository.Items[1].Id, "inglés", null, null, 0), CancellationToken.None));

        Assert.Equal(CatalogErrors.NameDuplicate, Assert.Single(exception.Issues).Code);
        Assert.Equal("Francés", repository.Items[1].NameEs);
    }

    [Fact]
    public async Task Update_allows_a_value_to_keep_its_own_name()
    {
        var repository = SeededRepository();
        var handler = new UpdateCatalogItemHandler(repository, Actor.Manager);

        var updated = await handler.Handle(
            new(Family, repository.Items[0].Id, "Inglés", null, "English", 0),
            CancellationToken.None);

        Assert.Equal("English", updated.NameEn);
    }

    [Fact]
    public async Task Update_reports_a_missing_value_with_the_Spanish_message()
    {
        var handler = new UpdateCatalogItemHandler(SeededRepository(), Actor.Manager);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new(Family, Guid.NewGuid(), "Alemán", null, null, 0), CancellationToken.None));

        Assert.Equal(CatalogErrors.NotFound, exception.Code);
        Assert.Equal("No se encontró el elemento del catálogo.", exception.Message);
    }

    [Fact]
    public async Task Update_reports_a_stale_version_as_a_conflict()
    {
        var repository = SeededRepository();
        repository.NextOutcome = CatalogSaveOutcome.ConcurrencyConflict;
        var handler = new UpdateCatalogItemHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new(Family, repository.Items[0].Id, "Inglés", null, null, 7), CancellationToken.None));

        Assert.Equal(CatalogErrors.ConcurrencyConflict, exception.Code);
        Assert.Equal(7u, repository.ExpectedVersion);
    }

    [Fact]
    public async Task Update_denies_a_reader()
    {
        var repository = SeededRepository();
        var handler = new UpdateCatalogItemHandler(repository, Actor.Reader);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, repository.Items[0].Id, "Otro", null, null, 0), CancellationToken.None));
        Assert.Null(repository.LastAuditEventType);
    }

    // ---- reorder ----

    [Fact]
    public async Task Reorder_rewrites_the_family_with_consecutive_positions()
    {
        var repository = SeededRepository();
        var handler = new ReorderCatalogFamilyHandler(repository, Actor.Manager);

        var result = await handler.Handle(
            new(Family, [repository.Items[1].Id, repository.Items[0].Id]),
            CancellationToken.None);

        Assert.Equal(["Francés", "Inglés"], result.Select(item => item.NameEs));
        Assert.Equal([1, 2], result.Select(item => item.SortOrder));
        Assert.Equal(CatalogAuditEvents.Reordered, repository.LastAuditEventType);
    }

    [Fact]
    public async Task Reorder_rejects_an_order_that_omits_a_value()
    {
        var repository = SeededRepository();
        var handler = new ReorderCatalogFamilyHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, [repository.Items[0].Id]), CancellationToken.None));

        Assert.Equal(CatalogErrors.ReorderIncomplete, Assert.Single(exception.Issues).Code);
        Assert.Equal(1, repository.Items[0].SortOrder);
        Assert.Null(repository.LastAuditEventType);
    }

    [Fact]
    public async Task Reorder_rejects_an_order_naming_a_foreign_value()
    {
        var repository = SeededRepository();
        var handler = new ReorderCatalogFamilyHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, [repository.Items[0].Id, Guid.NewGuid()]), CancellationToken.None));

        Assert.Equal(CatalogErrors.ReorderIncomplete, Assert.Single(exception.Issues).Code);
    }

    [Fact]
    public async Task Reorder_rejects_a_repeated_identifier()
    {
        var repository = SeededRepository();
        var handler = new ReorderCatalogFamilyHandler(repository, Actor.Manager);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => handler.Handle(new(Family, [repository.Items[0].Id, repository.Items[0].Id]), CancellationToken.None));
    }

    [Fact]
    public async Task Reorder_denies_a_reader()
    {
        var repository = SeededRepository();
        var handler = new ReorderCatalogFamilyHandler(repository, Actor.Reader);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, [repository.Items[0].Id]), CancellationToken.None));
    }

    // ---- activation ----

    [Fact]
    public async Task Deactivation_is_logical_and_keeps_the_value()
    {
        var repository = SeededRepository();
        var target = repository.Items[0];
        var handler = new SetCatalogItemActiveHandler(repository, Actor.Manager);

        var updated = await handler.Handle(new(Family, target.Id, false, 0), CancellationToken.None);

        Assert.False(updated.IsActive);
        Assert.Contains(target, repository.Items);
        Assert.Equal(CatalogAuditEvents.ActivationChanged, repository.LastAuditEventType);
    }

    [Fact]
    public async Task Reactivation_restores_the_value_in_its_position()
    {
        var repository = SeededRepository();
        var target = repository.Items[0];
        target.SetActive(false, DateTimeOffset.UtcNow);
        var handler = new SetCatalogItemActiveHandler(repository, Actor.Manager);

        var updated = await handler.Handle(new(Family, target.Id, true, 0), CancellationToken.None);

        Assert.True(updated.IsActive);
        Assert.Equal(1, updated.SortOrder);
    }

    [Fact]
    public async Task Activation_reports_a_missing_value()
    {
        var handler = new SetCatalogItemActiveHandler(SeededRepository(), Actor.Manager);
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new(Family, Guid.NewGuid(), false, 0), CancellationToken.None));
        Assert.Equal(CatalogErrors.NotFound, exception.Code);
    }

    [Fact]
    public async Task Activation_reports_a_stale_version_as_a_conflict()
    {
        var repository = SeededRepository();
        repository.NextOutcome = CatalogSaveOutcome.ConcurrencyConflict;
        var handler = new SetCatalogItemActiveHandler(repository, Actor.Manager);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => handler.Handle(new(Family, repository.Items[0].Id, false, 3), CancellationToken.None));

        Assert.Equal(CatalogErrors.ConcurrencyConflict, exception.Code);
    }

    [Fact]
    public async Task Activation_denies_a_reader_and_stores_nothing()
    {
        var repository = SeededRepository();
        var handler = new SetCatalogItemActiveHandler(repository, Actor.Reader);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, repository.Items[0].Id, false, 0), CancellationToken.None));

        Assert.True(repository.Items[0].IsActive);
        Assert.Null(repository.LastAuditEventType);
    }

    [Fact]
    public async Task Activation_denies_an_unauthenticated_actor()
    {
        var repository = SeededRepository();
        var handler = new SetCatalogItemActiveHandler(repository, Actor.Unauthenticated);
        await Assert.ThrowsAsync<ForbiddenException>(
            () => handler.Handle(new(Family, repository.Items[0].Id, false, 0), CancellationToken.None));
    }

    private static StubCatalogRepository SeededRepository()
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new StubCatalogRepository(
        [
            new CatalogItem(Guid.NewGuid(), Family, "INGLES", "Inglés", null, 1, createdAtUtc),
            new CatalogItem(Guid.NewGuid(), Family, "FRANCES", "Francés", null, 2, createdAtUtc),
        ]);
    }

    private sealed class StubCatalogRepository(List<CatalogItem> items) : ICatalogRepository
    {
        public List<CatalogItem> Items { get; } = items;
        public string? LastAuditEventType { get; private set; }
        public string? LastAuditSubjectId { get; private set; }
        public uint? ExpectedVersion { get; private set; }
        public CatalogSaveOutcome NextOutcome { get; set; } = CatalogSaveOutcome.Saved;
        public bool ValueIsInUse { get; set; }

        public Task<IReadOnlyList<CatalogItem>> ListAsync(
            string family,
            bool includeInactive,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CatalogItem>>(Items
                .Where(item => item.Family == family && (includeInactive || item.IsActive))
                .OrderBy(item => item.SortOrder)
                .ToArray());

        public Task<CatalogItem?> FindAsync(string family, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Family == family && item.Id == id));

        public void Add(CatalogItem item) => Items.Add(item);

        public void ExpectVersion(CatalogItem item, uint version) => ExpectedVersion = version;

        public Task<CatalogSaveOutcome> SaveAsync(
            string auditEventType,
            string subjectId,
            CancellationToken cancellationToken)
        {
            if (NextOutcome == CatalogSaveOutcome.Saved)
            {
                LastAuditEventType = auditEventType;
                LastAuditSubjectId = subjectId;
            }
            return Task.FromResult(NextOutcome);
        }

        public Task<bool> IsValueInUseAsync(string family, string nameNormalized, CancellationToken cancellationToken) =>
            Task.FromResult(ValueIsInUse);
    }

    private sealed class Actor(bool authenticated, params string[] permissions) : ICurrentActor
    {
        public static Actor Manager => new(true, Permissions.CatalogsRead, Permissions.CatalogsManage);
        public static Actor Reader => new(true, Permissions.CatalogsRead);
        public static Actor None => new(true);
        public static Actor Unauthenticated => new(false, Permissions.CatalogsRead, Permissions.CatalogsManage);

        public string? ExternalKey => authenticated ? "test-actor" : null;
        public bool IsAuthenticated => authenticated;
        public bool HasPermission(string permission) =>
            authenticated && permissions.Contains(permission, StringComparer.Ordinal);
    }
}
