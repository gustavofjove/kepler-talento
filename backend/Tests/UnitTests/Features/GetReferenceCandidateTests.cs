using FluentValidation;
using KeplerTalento.Application.Abstractions.Identity;
using KeplerTalento.Application.Abstractions.Persistence;
using KeplerTalento.Application.Common.Errors;
using KeplerTalento.Application.Features.Candidates;
using KeplerTalento.Domain.Candidates;
using Xunit;

namespace KeplerTalento.Tests.UnitTests.Features;

public sealed class GetReferenceCandidateTests
{
    [Fact]
    public async Task Returns_synthetic_candidate_for_permitted_actor()
    {
        var candidate = new Candidate(Guid.NewGuid(), "Candidata", "Sintética", DateTimeOffset.UtcNow);
        var handler = new GetReferenceCandidateHandler(new StubReader(candidate), new StubActor(true));

        var result = await handler.Handle(new(candidate.Id), CancellationToken.None);

        Assert.Equal(candidate.Id, result.Id);
        Assert.Equal("Candidata", result.FirstName);
    }

    [Fact]
    public async Task Denies_missing_actor_before_reading_candidate()
    {
        var handler = new GetReferenceCandidateHandler(new StubReader(null), new StubActor(false));
        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Returns_stable_not_found_exception()
    {
        var handler = new GetReferenceCandidateHandler(new StubReader(null), new StubActor(true));
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal("candidate.not_found", exception.Code);
    }

    [Fact]
    public async Task Validator_rejects_empty_identifier_with_stable_code()
    {
        IValidator<GetReferenceCandidateQuery> validator = new GetReferenceCandidateValidator();
        var result = await validator.ValidateAsync(new(Guid.Empty));
        Assert.Equal("candidate.id.required", Assert.Single(result.Errors).ErrorCode);
    }

    private sealed class StubReader(Candidate? candidate) : ICandidateReader
    {
        public Task<Candidate?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(candidate);
    }

    private sealed class StubActor(bool permitted) : ICurrentActor
    {
        public string? ExternalKey => permitted ? "test" : null;
        public bool IsAuthenticated => permitted;
        public bool HasPermission(string permission) => permitted && permission == Permissions.CandidatesRead;
    }
}
